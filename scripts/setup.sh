#!/usr/bin/env bash
#
# DarkVelocity POS - Local Development Environment Setup
#
# This script sets up the complete local development environment including:
# - Docker infrastructure (PostgreSQL, Azurite, SpiceDB)
# - .NET backend services build
# - Node.js frontend apps dependencies
#

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# Configuration
DOCKER_COMPOSE_FILE="$PROJECT_ROOT/docker/docker-compose.yml"
AZURITE_WAIT_TIMEOUT=30
POSTGRES_WAIT_TIMEOUT=60

# -----------------------------------------------------------------------------
# Helper Functions
# -----------------------------------------------------------------------------

print_header() {
    echo ""
    echo -e "${BLUE}========================================${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}========================================${NC}"
    echo ""
}

print_success() {
    echo -e "${GREEN}[OK]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

print_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

check_command() {
    if command -v "$1" &> /dev/null; then
        return 0
    else
        return 1
    fi
}

# -----------------------------------------------------------------------------
# Prerequisite Checks
# -----------------------------------------------------------------------------

check_prerequisites() {
    print_header "Checking Prerequisites"

    local missing_prereqs=0

    # Check Docker
    if check_command docker; then
        local docker_version=$(docker --version | cut -d' ' -f3 | cut -d',' -f1)
        print_success "Docker installed (version $docker_version)"
    else
        print_error "Docker is not installed"
        echo "  Install from: https://docs.docker.com/get-docker/"
        missing_prereqs=1
    fi

    # Check Docker Compose
    if docker compose version &> /dev/null; then
        local compose_version=$(docker compose version --short 2>/dev/null || echo "unknown")
        print_success "Docker Compose installed (version $compose_version)"
    elif check_command docker-compose; then
        local compose_version=$(docker-compose --version | cut -d' ' -f4 | cut -d',' -f1)
        print_success "Docker Compose (legacy) installed (version $compose_version)"
    else
        print_error "Docker Compose is not installed"
        echo "  Install from: https://docs.docker.com/compose/install/"
        missing_prereqs=1
    fi

    # Check .NET SDK
    if check_command dotnet; then
        local dotnet_version=$(dotnet --version)
        print_success ".NET SDK installed (version $dotnet_version)"

        # Check for .NET 10
        if [[ ! "$dotnet_version" =~ ^10\. ]]; then
            print_warning "Project requires .NET 10, but version $dotnet_version is installed"
            echo "  Download .NET 10 from: https://dotnet.microsoft.com/download/dotnet/10.0"
        fi
    else
        print_error ".NET SDK is not installed"
        echo "  Download from: https://dotnet.microsoft.com/download/dotnet/10.0"
        missing_prereqs=1
    fi

    # Check Node.js
    if check_command node; then
        local node_version=$(node --version)
        print_success "Node.js installed (version $node_version)"

        # Check for Node.js 20+
        local node_major=$(echo "$node_version" | cut -d'.' -f1 | tr -d 'v')
        if [[ "$node_major" -lt 20 ]]; then
            print_warning "Project recommends Node.js 20+, but version $node_version is installed"
        fi
    else
        print_error "Node.js is not installed"
        echo "  Download from: https://nodejs.org/"
        missing_prereqs=1
    fi

    # Check npm
    if check_command npm; then
        local npm_version=$(npm --version)
        print_success "npm installed (version $npm_version)"
    else
        print_error "npm is not installed"
        missing_prereqs=1
    fi

    if [[ $missing_prereqs -eq 1 ]]; then
        echo ""
        print_error "Missing required prerequisites. Please install them and try again."
        exit 1
    fi

    echo ""
    print_success "All prerequisites satisfied!"
}

# -----------------------------------------------------------------------------
# Docker Infrastructure
# -----------------------------------------------------------------------------

start_docker_infrastructure() {
    print_header "Starting Docker Infrastructure"

    # Check if Docker daemon is running
    if ! docker info &> /dev/null; then
        print_error "Docker daemon is not running. Please start Docker and try again."
        exit 1
    fi

    print_info "Starting containers..."
    cd "$PROJECT_ROOT/docker"

    # Use docker compose (new) or docker-compose (legacy)
    if docker compose version &> /dev/null; then
        docker compose up -d
    else
        docker-compose up -d
    fi

    cd "$PROJECT_ROOT"
    print_success "Docker containers started"
}

wait_for_postgres() {
    print_info "Waiting for PostgreSQL to be ready..."

    local elapsed=0
    while [[ $elapsed -lt $POSTGRES_WAIT_TIMEOUT ]]; do
        if docker exec darkvelocity-postgres pg_isready -U darkvelocity &> /dev/null; then
            print_success "PostgreSQL is ready"
            return 0
        fi
        sleep 2
        elapsed=$((elapsed + 2))
        echo -ne "\r  Waiting... ${elapsed}s"
    done

    echo ""
    print_error "PostgreSQL did not become ready within ${POSTGRES_WAIT_TIMEOUT}s"
    return 1
}

wait_for_azurite() {
    print_info "Waiting for Azurite (Azure Storage Emulator) to be ready..."

    local elapsed=0
    while [[ $elapsed -lt $AZURITE_WAIT_TIMEOUT ]]; do
        if nc -z localhost 10002 &> /dev/null; then
            print_success "Azurite is ready"
            return 0
        fi
        sleep 2
        elapsed=$((elapsed + 2))
        echo -ne "\r  Waiting... ${elapsed}s"
    done

    echo ""
    print_error "Azurite did not become ready within ${AZURITE_WAIT_TIMEOUT}s"
    return 1
}

wait_for_services() {
    print_header "Waiting for Infrastructure Services"

    wait_for_azurite
    wait_for_postgres

    echo ""
    print_success "All infrastructure services are ready!"
}

# -----------------------------------------------------------------------------
# Backend Setup
# -----------------------------------------------------------------------------

build_dotnet_projects() {
    print_header "Building .NET Projects"

    cd "$PROJECT_ROOT"

    print_info "Restoring NuGet packages..."
    dotnet restore

    print_info "Building solution..."
    dotnet build --no-restore

    print_success ".NET projects built successfully"
}

run_dotnet_tests() {
    print_header "Running .NET Tests"

    cd "$PROJECT_ROOT"

    print_info "Running tests..."
    dotnet test --no-build --verbosity minimal

    print_success "All tests passed!"
}

# -----------------------------------------------------------------------------
# Frontend Setup
# -----------------------------------------------------------------------------

install_frontend_dependencies() {
    print_header "Installing Frontend Dependencies"

    local apps_dir="$PROJECT_ROOT/apps"
    local apps=("pos" "backoffice" "kds")

    for app in "${apps[@]}"; do
        local app_dir="$apps_dir/$app"
        if [[ -d "$app_dir" ]] && [[ -f "$app_dir/package.json" ]]; then
            print_info "Installing dependencies for $app..."
            cd "$app_dir"
            npm install
            print_success "$app dependencies installed"
        else
            print_warning "App $app not found at $app_dir"
        fi
    done

    cd "$PROJECT_ROOT"
    print_success "All frontend dependencies installed"
}

# -----------------------------------------------------------------------------
# Status and Information
# -----------------------------------------------------------------------------

show_status() {
    print_header "Development Environment Status"

    echo "Docker Containers:"
    echo "-----------------"
    docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}" | grep -E "darkvelocity|NAMES" || echo "  No containers running"

    echo ""
    echo "Service Ports:"
    echo "--------------"
    echo "  Azurite Blob:   localhost:10000"
    echo "  Azurite Queue:  localhost:10001"
    echo "  Azurite Table:  localhost:10002"
    echo "  PostgreSQL:     localhost:5432"
    echo "  SpiceDB gRPC:   localhost:50051"
    echo "  SpiceDB HTTP:   localhost:8443"
    echo ""
    echo "Database Connection:"
    echo "--------------------"
    echo "  Host:     localhost"
    echo "  Port:     5432"
    echo "  User:     darkvelocity"
    echo "  Password: darkvelocity_dev"
    echo "  Database: darkvelocity (main)"
    echo ""
    echo "Service Databases:"
    echo "------------------"
    echo "  auth_db, location_db, menu_db, orders_db, payments_db,"
    echo "  hardware_db, inventory_db, procurement_db, costing_db, reporting_db"
}

show_next_steps() {
    print_header "Setup Complete - Next Steps"

    echo "To run a backend service:"
    echo "  cd src/Services/<ServiceName>/<ServiceName>.Api"
    echo "  dotnet run"
    echo ""
    echo "  Services and their ports:"
    echo "    Auth:        5000    Location:    5001"
    echo "    Menu:        5002    Orders:      5003"
    echo "    Payments:    5004    Hardware:    5005"
    echo "    Inventory:   5006    Procurement: 5007"
    echo "    Costing:     5008    Reporting:   5009"
    echo "    API Gateway: 8000"
    echo ""
    echo "To run a frontend app:"
    echo "  cd apps/<app>  # pos, backoffice, or kds"
    echo "  npm run dev"
    echo ""
    echo "To run all tests:"
    echo "  dotnet test"
    echo ""
    echo "To stop Docker infrastructure:"
    echo "  cd docker && docker compose down"
    echo ""
    echo "To view logs:"
    echo "  docker logs -f darkvelocity-postgres"
    echo "  docker logs -f darkvelocity-azurite"
    echo ""
}

# -----------------------------------------------------------------------------
# Main Script
# -----------------------------------------------------------------------------

show_usage() {
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  --check-only     Only check prerequisites, don't setup"
    echo "  --skip-docker    Skip Docker infrastructure setup"
    echo "  --skip-backend   Skip .NET build"
    echo "  --skip-frontend  Skip frontend npm install"
    echo "  --run-tests      Run tests after build"
    echo "  --status         Show current environment status"
    echo "  -h, --help       Show this help message"
    echo ""
}

main() {
    local check_only=false
    local skip_docker=false
    local skip_backend=false
    local skip_frontend=false
    local run_tests=false
    local show_status_only=false

    # Parse arguments
    while [[ $# -gt 0 ]]; do
        case "$1" in
            --check-only)
                check_only=true
                shift
                ;;
            --skip-docker)
                skip_docker=true
                shift
                ;;
            --skip-backend)
                skip_backend=true
                shift
                ;;
            --skip-frontend)
                skip_frontend=true
                shift
                ;;
            --run-tests)
                run_tests=true
                shift
                ;;
            --status)
                show_status_only=true
                shift
                ;;
            -h|--help)
                show_usage
                exit 0
                ;;
            *)
                print_error "Unknown option: $1"
                show_usage
                exit 1
                ;;
        esac
    done

    echo ""
    echo -e "${BLUE}  ____             _  __     __     _            _ _         ${NC}"
    echo -e "${BLUE} |  _ \\  __ _ _ __| | \\ \\   / /___ | | ___   ___(_) |_ _   _ ${NC}"
    echo -e "${BLUE} | | | |/ _\` | '__| |/ \\ \\ / // _ \\| |/ _ \\ / __| | __| | | |${NC}"
    echo -e "${BLUE} | |_| | (_| | |  |   <  \\ V /  __/| | (_) | (__| | |_| |_| |${NC}"
    echo -e "${BLUE} |____/ \\__,_|_|  |_|\\_\\  \\_/ \\___||_|\\___/ \\___|_|\\__|\\__, |${NC}"
    echo -e "${BLUE}                                                       |___/ ${NC}"
    echo -e "${BLUE}                    POS Development Setup${NC}"
    echo ""

    if [[ "$show_status_only" == true ]]; then
        show_status
        exit 0
    fi

    # Always check prerequisites
    check_prerequisites

    if [[ "$check_only" == true ]]; then
        exit 0
    fi

    # Setup steps
    if [[ "$skip_docker" != true ]]; then
        start_docker_infrastructure
        wait_for_services
    fi

    if [[ "$skip_backend" != true ]]; then
        build_dotnet_projects

        if [[ "$run_tests" == true ]]; then
            run_dotnet_tests
        fi
    fi

    if [[ "$skip_frontend" != true ]]; then
        install_frontend_dependencies
    fi

    # Show final status
    show_status
    show_next_steps
}

# Run main function with all arguments
main "$@"
