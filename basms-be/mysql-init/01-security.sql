-- ================================================
-- MySQL Security Hardening Script
-- ================================================
-- This script will be executed when MySQL container starts for the first time

-- Remove anonymous users
DELETE FROM mysql.user WHERE User='';

-- Remove test database if exists
DROP DATABASE IF EXISTS test;
DELETE FROM mysql.db WHERE Db='test' OR Db='test\\_%';

-- Only allow root login from localhost (inside container)
DELETE FROM mysql.user WHERE User='root' AND Host NOT IN ('localhost', '127.0.0.1', '::1');

-- Update root password policy (if needed)
-- ALTER USER 'root'@'localhost' IDENTIFIED WITH mysql_native_password BY '${MYSQL_ROOT_PASSWORD}';

-- Flush privileges to apply changes
FLUSH PRIVILEGES;

-- Display success message
SELECT 'MySQL security hardening completed successfully!' AS Status;
