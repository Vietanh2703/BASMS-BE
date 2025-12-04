-- ================================================
-- Verify and Fix MySQL Permissions
-- ================================================
-- This script can be run manually if you encounter permission issues
-- after the initial setup

-- Show current users and their hosts
SELECT User, Host, plugin FROM mysql.user WHERE User IN ('root', 'basms_user');

-- Show current grants for basms_user
SHOW GRANTS FOR 'basms_user'@'%';

-- Verify databases exist
SHOW DATABASES LIKE 'basms_%';

-- Display success message
SELECT 'Permission verification completed!' AS Status;
