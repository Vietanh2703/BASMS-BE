-- ================================================
-- FIX MySQL Permissions - Manual Script
-- ================================================
-- Run this script if you encounter MySQL permission errors
--
-- HOW TO USE:
-- 1. Copy this file to your VPS
-- 2. Run: docker exec -i basms-mysql mysql -u root -p < fix-mysql-permissions.sql
-- 3. Enter your MYSQL_ROOT_PASSWORD when prompted
--
-- Or connect to MySQL and paste the commands below:
-- docker exec -it basms-mysql mysql -u root -p
-- ================================================

-- Create databases if they don't exist
CREATE DATABASE IF NOT EXISTS basms_users CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE IF NOT EXISTS basms_contracts CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE IF NOT EXISTS basms_shifts CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- Grant all privileges to basms_user from any host
-- Note: Replace 'YOUR_PASSWORD' with your actual MYSQL_USER_PASSWORD
-- Or if basms_user already exists, just grant permissions
GRANT ALL PRIVILEGES ON basms_users.* TO 'basms_user'@'%';
GRANT ALL PRIVILEGES ON basms_contracts.* TO 'basms_user'@'%';
GRANT ALL PRIVILEGES ON basms_shifts.* TO 'basms_user'@'%';

-- Flush privileges to apply changes immediately
FLUSH PRIVILEGES;

-- Verify the grants
SHOW GRANTS FOR 'basms_user'@'%';

-- Display databases
SHOW DATABASES;

-- Display success message
SELECT '✓ MySQL permissions fixed successfully!' AS Status;
SELECT 'basms_user can now connect from any Docker container' AS Info;
