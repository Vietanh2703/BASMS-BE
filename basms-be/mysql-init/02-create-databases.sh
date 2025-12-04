#!/bin/bash
set -e

echo "================================================"
echo "Create Databases and User for BASMS Application"
echo "================================================"

# Get password from environment variable or use empty string as fallback
MYSQL_PASSWORD="${MYSQL_PASSWORD:-${MYSQL_USER_PASSWORD:-}}"

# If password is still empty, generate a default one (should not happen in production)
if [ -z "$MYSQL_PASSWORD" ]; then
  echo "WARNING: MYSQL_USER_PASSWORD not set, using MYSQL_ROOT_PASSWORD for basms_user"
  MYSQL_PASSWORD="$MYSQL_ROOT_PASSWORD"
fi

echo "Creating databases and user..."

# Execute SQL commands
mysql -u root -p"${MYSQL_ROOT_PASSWORD}" <<-EOSQL
  -- Create databases if they don't exist
  CREATE DATABASE IF NOT EXISTS basms_users CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
  CREATE DATABASE IF NOT EXISTS basms_contracts CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
  CREATE DATABASE IF NOT EXISTS basms_shifts CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

  -- Create user if not exists (MySQL 8.0+ syntax)
  -- Using '%' allows connections from any host (needed for Docker network)
  CREATE USER IF NOT EXISTS 'basms_user'@'%' IDENTIFIED BY '${MYSQL_PASSWORD}';

  -- Grant all privileges to basms_user on all BASMS databases
  GRANT ALL PRIVILEGES ON basms_users.* TO 'basms_user'@'%';
  GRANT ALL PRIVILEGES ON basms_contracts.* TO 'basms_user'@'%';
  GRANT ALL PRIVILEGES ON basms_shifts.* TO 'basms_user'@'%';

  -- Flush privileges to apply changes
  FLUSH PRIVILEGES;

  -- Display databases
  SHOW DATABASES;

  SELECT 'Databases created successfully!' AS Status;
EOSQL

echo "✅ Database initialization completed!"
