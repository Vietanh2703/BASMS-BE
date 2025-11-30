-- ============================================================================
-- Database Initialization Script for BASMS
-- Creates users_db, contracts_db, and shifts_db databases
-- ============================================================================

-- Create all databases (users_db is created automatically by MYSQL_DATABASE env var)
CREATE DATABASE IF NOT EXISTS contracts_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE IF NOT EXISTS shifts_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- Grant privileges for all databases
GRANT ALL PRIVILEGES ON users_db.* TO 'root'@'%';
GRANT ALL PRIVILEGES ON contracts_db.* TO 'root'@'%';
GRANT ALL PRIVILEGES ON shifts_db.* TO 'root'@'%';
FLUSH PRIVILEGES;

-- Log initialization
SELECT 'Database initialization completed successfully' AS status;
SELECT CONCAT('Created databases: users_db, contracts_db, shifts_db') AS databases;
