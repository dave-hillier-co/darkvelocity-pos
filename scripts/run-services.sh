#!/usr/bin/env bash
#
# DarkVelocity POS - Run Backend Services
#
# This script starts the Orleans Silo and API Gateway.
#

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# Services configuration
declare -A SERVICES
SERVICES["orleans-silo"]="src/Services/Orleans/Orleans.Silo"
SERVICES["gateway"]="src/Gateway/ApiGateway"

declare -A SERVICE_PORTS
SERVICE_PORTS["orleans-silo"]=5200
SERVICE_PORTS["gateway"]=5000

ALL_SERVICES=("orleans-silo" "gateway")

# PID file location
PID_DIR="$PROJECT_ROOT/.run"
LOG_DIR="$PROJECT_ROOT/.run/logs"

print_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[OK]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

show_usage() {
    echo "Usage: $0 [COMMAND] [OPTIONS]"
    echo ""
    echo "Commands:"
    echo "  start [services...]  Start services (default: all)"
    echo "  stop [services...]   Stop services (default: all)"
    echo "  status               Show status of all services"
    echo "  logs <service>       Tail logs for a service"
    echo ""
    echo "Options:"
    echo "  -h, --help           Show this help message"
    echo ""
    echo "Examples:"
    echo "  $0 start                        # Start all services"
    echo "  $0 start orleans-silo           # Start Orleans Silo only"
    echo "  $0 start gateway                # Start API Gateway only"
    echo "  $0 stop                         # Stop all services"
    echo "  $0 status                       # Show status"
    echo "  $0 logs orleans-silo            # View Orleans Silo logs"
    echo ""
    echo "Available services:"
    echo "  orleans-silo  - Orleans Silo (all backend APIs, port 5200)"
    echo "  gateway       - API Gateway (port 5000)"
    echo ""
    echo "Additional endpoints:"
    echo "  Orleans Dashboard: http://localhost:8888"
    echo "  Swagger UI:        http://localhost:5200/swagger"
    echo ""
}

ensure_dirs() {
    mkdir -p "$PID_DIR"
    mkdir -p "$LOG_DIR"
}

get_pid_file() {
    echo "$PID_DIR/$1.pid"
}

get_log_file() {
    echo "$LOG_DIR/$1.log"
}

is_service_running() {
    local service=$1
    local pid_file=$(get_pid_file "$service")

    if [[ -f "$pid_file" ]]; then
        local pid=$(cat "$pid_file")
        if kill -0 "$pid" 2>/dev/null; then
            return 0
        fi
    fi
    return 1
}

start_service() {
    local service=$1
    local service_dir="${SERVICES[$service]}"
    local port=${SERVICE_PORTS[$service]}
    local pid_file=$(get_pid_file "$service")
    local log_file=$(get_log_file "$service")

    if [[ -z "$service_dir" ]]; then
        print_error "Unknown service: $service"
        return 1
    fi

    local full_path="$PROJECT_ROOT/$service_dir"
    if [[ ! -d "$full_path" ]]; then
        print_error "Service directory not found: $full_path"
        return 1
    fi

    if is_service_running "$service"; then
        print_warning "$service is already running (PID: $(cat "$pid_file"))"
        return 0
    fi

    print_info "Starting $service on port $port..."

    cd "$full_path"
    ASPNETCORE_URLS="http://localhost:$port" \
        nohup dotnet run --no-build > "$log_file" 2>&1 &
    local pid=$!
    echo "$pid" > "$pid_file"

    # Wait a moment and check if it started
    sleep 2
    if kill -0 "$pid" 2>/dev/null; then
        print_success "$service started (PID: $pid, Port: $port)"
    else
        print_error "$service failed to start. Check logs: $log_file"
        rm -f "$pid_file"
        return 1
    fi
}

stop_service() {
    local service=$1
    local pid_file=$(get_pid_file "$service")

    if [[ ! -f "$pid_file" ]]; then
        print_warning "$service is not running"
        return 0
    fi

    local pid=$(cat "$pid_file")
    if kill -0 "$pid" 2>/dev/null; then
        print_info "Stopping $service (PID: $pid)..."
        kill "$pid"

        # Wait for graceful shutdown
        local count=0
        while kill -0 "$pid" 2>/dev/null && [[ $count -lt 10 ]]; do
            sleep 1
            count=$((count + 1))
        done

        if kill -0 "$pid" 2>/dev/null; then
            print_warning "Force killing $service..."
            kill -9 "$pid" 2>/dev/null || true
        fi

        print_success "$service stopped"
    fi

    rm -f "$pid_file"
}

show_status() {
    echo ""
    echo "Service Status:"
    echo "---------------"
    printf "%-15s %-8s %-8s %s\n" "SERVICE" "STATUS" "PORT" "PID"
    echo "---------------------------------------"

    for service in "${ALL_SERVICES[@]}"; do
        local port=${SERVICE_PORTS[$service]}
        local pid_file=$(get_pid_file "$service")

        if is_service_running "$service"; then
            local pid=$(cat "$pid_file")
            printf "%-15s ${GREEN}%-8s${NC} %-8s %s\n" "$service" "running" "$port" "$pid"
        else
            printf "%-15s ${RED}%-8s${NC} %-8s %s\n" "$service" "stopped" "$port" "-"
        fi
    done
    echo ""
}

tail_logs() {
    local service=$1
    local log_file=$(get_log_file "$service")

    if [[ ! -f "$log_file" ]]; then
        print_error "No log file found for $service"
        return 1
    fi

    echo "Tailing logs for $service (Ctrl+C to stop)..."
    tail -f "$log_file"
}

start_services() {
    local services=("$@")
    if [[ ${#services[@]} -eq 0 ]]; then
        services=("${ALL_SERVICES[@]}")
    fi

    ensure_dirs

    # Build first
    print_info "Building solution..."
    cd "$PROJECT_ROOT"
    dotnet build --verbosity quiet

    echo ""
    # Start orleans-silo first if in the list
    for service in "${services[@]}"; do
        if [[ "$service" == "orleans-silo" ]]; then
            start_service "orleans-silo"
            # Give Orleans time to fully start before starting gateway
            sleep 3
            break
        fi
    done

    # Start remaining services
    for service in "${services[@]}"; do
        if [[ "$service" != "orleans-silo" ]]; then
            start_service "$service"
        fi
    done

    echo ""
    show_status
}

stop_services() {
    local services=("$@")
    if [[ ${#services[@]} -eq 0 ]]; then
        services=("gateway" "orleans-silo")
    fi

    for service in "${services[@]}"; do
        stop_service "$service"
    done

    echo ""
    show_status
}

main() {
    if [[ $# -eq 0 ]]; then
        show_usage
        exit 0
    fi

    local command=$1
    shift

    case "$command" in
        start)
            start_services "$@"
            ;;
        stop)
            stop_services "$@"
            ;;
        status)
            ensure_dirs
            show_status
            ;;
        logs)
            if [[ $# -eq 0 ]]; then
                print_error "Please specify a service name"
                exit 1
            fi
            tail_logs "$1"
            ;;
        -h|--help)
            show_usage
            ;;
        *)
            print_error "Unknown command: $command"
            show_usage
            exit 1
            ;;
    esac
}

main "$@"
