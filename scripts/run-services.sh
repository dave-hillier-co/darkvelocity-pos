#!/usr/bin/env bash
#
# DarkVelocity POS - Run Backend Services
#
# This script starts backend services in the background.
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

# All available services
ALL_SERVICES=("Auth" "Location" "Menu" "Orders" "Payments" "Hardware" "Inventory" "Procurement" "Costing" "Reporting")

# Service ports
declare -A SERVICE_PORTS
SERVICE_PORTS["Auth"]=5000
SERVICE_PORTS["Location"]=5001
SERVICE_PORTS["Menu"]=5002
SERVICE_PORTS["Orders"]=5003
SERVICE_PORTS["Payments"]=5004
SERVICE_PORTS["Hardware"]=5005
SERVICE_PORTS["Inventory"]=5006
SERVICE_PORTS["Procurement"]=5007
SERVICE_PORTS["Costing"]=5008
SERVICE_PORTS["Reporting"]=5009

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
    echo "  $0 start                    # Start all services"
    echo "  $0 start Menu Orders        # Start specific services"
    echo "  $0 stop                     # Stop all services"
    echo "  $0 status                   # Show status"
    echo "  $0 logs Menu                # View Menu service logs"
    echo ""
    echo "Available services:"
    echo "  ${ALL_SERVICES[*]}"
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
    local port=${SERVICE_PORTS[$service]}
    local service_dir="$PROJECT_ROOT/src/Services/$service/$service.Api"
    local pid_file=$(get_pid_file "$service")
    local log_file=$(get_log_file "$service")

    if [[ ! -d "$service_dir" ]]; then
        print_error "Service directory not found: $service_dir"
        return 1
    fi

    if is_service_running "$service"; then
        print_warning "$service is already running (PID: $(cat "$pid_file"))"
        return 0
    fi

    print_info "Starting $service on port $port..."

    cd "$service_dir"
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
    for service in "${services[@]}"; do
        start_service "$service"
    done

    echo ""
    show_status
}

stop_services() {
    local services=("$@")
    if [[ ${#services[@]} -eq 0 ]]; then
        services=("${ALL_SERVICES[@]}")
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
