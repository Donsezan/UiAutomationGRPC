#!/bin/bash

# Directory to store certificates
CERT_DIR="./certs"
mkdir -p $CERT_DIR

# Generate CA key and certificate
openssl genrsa -out $CERT_DIR/ca.key 4096
openssl req -new -x509 -days 3650 -key $CERT_DIR/ca.key -out $CERT_DIR/ca.crt -subj "/CN=UiAutomationGRPC-CA"

# Generate Server key and CSR
# Use a specific hostname if needed, e.g., /CN=my-server.corporate.local
HOSTNAME=${1:-localhost}
openssl genrsa -out $CERT_DIR/server.key 4096
openssl req -new -key $CERT_DIR/server.key -out $CERT_DIR/server.csr -subj "/CN=$HOSTNAME"

# Sign Server CSR with CA
openssl x509 -req -days 3650 -in $CERT_DIR/server.csr -CA $CERT_DIR/ca.crt -CAkey $CERT_DIR/ca.key -CAcreateserial -out $CERT_DIR/server.crt

echo "Certificates generated in $CERT_DIR"
