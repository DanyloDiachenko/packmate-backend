SELECT 'CREATE DATABASE packmate_users_db' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'packmate_users_db')\gexec
SELECT 'CREATE DATABASE packmate_trips_db' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'packmate_trips_db')\gexec
SELECT 'CREATE DATABASE packmate_trip_items_db' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'packmate_trip_items_db')\gexec
