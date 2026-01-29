#!/usr/bin/env bash
#
# DarkVelocity POS - Teardown Development Environment
#
# This script stops and optionally removes all Docker infrastructure.
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

print_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[OK]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

show_usage() {
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  --remove-volumes  Remove Docker volumes (deletes all data)"
    echo "  --remove-all      Remove containers, volumes, and images"
    echo "  -h, --help        Show this help message"
    echo ""
}

main() {
    local remove_volumes=false
    local remove_all=false

    while [[ $# -gt 0 ]]; do
        case "$1" in
            --remove-volumes)
                remove_volumes=true
                shift
                ;;
            --remove-all)
                remove_all=true
                shift
                ;;
            -h|--help)
                show_usage
                exit 0
                ;;
            *)
                echo "Unknown option: $1"
                show_usage
                exit 1
                ;;
        esac
    done

    cd "$PROJECT_ROOT/docker"

    if [[ "$remove_all" == true ]]; then
        print_warning "Removing all containers, volumes, and images..."
        if docker compose version &> /dev/null; then
            docker compose down -v --rmi all 2>/dev/null || true
        else
            docker-compose down -v --rmi all 2>/dev/null || true
        fi
        print_success "All Docker resources removed"
    elif [[ "$remove_volumes" == true ]]; then
        print_warning "Stopping containers and removing volumes..."
        if docker compose version &> /dev/null; then
            docker compose down -v
        else
            docker-compose down -v
        fi
        print_success "Containers stopped and volumes removed"
    else
        print_info "Stopping containers..."
        if docker compose version &> /dev/null; then
            docker compose down
        else
            docker-compose down
        fi
        print_success "Containers stopped (volumes preserved)"
    fi

    echo ""
    echo "To restart the environment, run:"
    echo "  ./scripts/setup.sh"
    echo ""
}

main "$@"
