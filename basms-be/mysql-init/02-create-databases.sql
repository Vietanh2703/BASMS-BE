-- ================================================
-- Create Databases for BASMS Application
-- ================================================

-- Create databases if they don't exist
CREATE DATABASE IF NOT EXISTS basms_users CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE IF NOT EXISTS basms_contracts CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE IF NOT EXISTS basms_shifts CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- Note: basms_user is automatically created by MYSQL_USER and MYSQL_PASSWORD env vars
-- in docker-compose.yml. This happens before these scripts run.

-- Grant privileges to basms_user on all databases
-- Using '%' allows connections from any host (needed for Docker network)
GRANT ALL PRIVILEGES ON basms_users.* TO 'basms_user'@'%';
GRANT ALL PRIVILEGES ON basms_contracts.* TO 'basms_user'@'%';
GRANT ALL PRIVILEGES ON basms_shifts.* TO 'basms_user'@'%';

-- Flush privileges
FLUSH PRIVILEGES;

-- Display databases
SHOW DATABASES;

SELECT 'Databases created successfully!' AS Status;
