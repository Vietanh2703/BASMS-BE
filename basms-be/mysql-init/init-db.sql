-- ============================================================================
-- Database Initialization Script for BASMS
-- Creates users_db, contracts_db, and shifts_db databases
-- Grants privileges to basms_user with minimal required permissions
-- ============================================================================

-- Create all databases (users_db is created automatically by MYSQL_DATABASE env var)
CREATE DATABASE IF NOT EXISTS users_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE IF NOT EXISTS contracts_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE IF NOT EXISTS shifts_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE IF NOT EXISTS attendances_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE IF NOT EXISTS incidents_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE IF NOT EXISTS chats_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- Grant privileges to basms_user for all application databases
-- Using specific privileges instead of ALL for better security
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, DROP, INDEX, ALTER, REFERENCES ON users_db.* TO 'basms_user'@'%';
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, DROP, INDEX, ALTER, REFERENCES ON contracts_db.* TO 'basms_user'@'%';
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, DROP, INDEX, ALTER, REFERENCES ON shifts_db.* TO 'basms_user'@'%';
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, DROP, INDEX, ALTER, REFERENCES ON attendances_db.* TO 'basms_user'@'%';
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, DROP, INDEX, ALTER, REFERENCES ON incidents_db.* TO 'basms_user'@'%';
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, DROP, INDEX, ALTER, REFERENCES ON chats_db.* TO 'basms_user'@'%';

-- Apply changes
FLUSH PRIVILEGES;

-- Log initialization
SELECT 'Database initialization completed successfully' AS status;
SELECT CONCAT('Databases created: users_db, contracts_db, shifts_db') AS databases;
SELECT CONCAT('Privileges granted to: basms_user') AS user_status;
