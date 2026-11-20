#!/usr/bin/env bash
set -e

echo "==================================================="
echo "              PROJECT INFINITY"
echo "==================================================="

# Check for Node.js
if ! command -v node &> /dev/null; then
    echo "[ERROR] Node.js is not installed or not in PATH."
    echo "Please install Node.js 22 or higher."
    exit 1
fi

echo "[1/2] Starting backend server on ws://127.0.0.1:8787..."
cd "$(dirname "$0")/server"
npm test
npm start
