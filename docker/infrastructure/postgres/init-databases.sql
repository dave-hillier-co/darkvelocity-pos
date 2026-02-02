-- DarkVelocity Database Initialization Script
-- Creates separate databases for each service (database-per-service pattern)

-- Auth Service Database
CREATE DATABASE auth_db;
GRANT ALL PRIVILEGES ON DATABASE auth_db TO darkvelocity;

-- Location Service Database
CREATE DATABASE location_db;
GRANT ALL PRIVILEGES ON DATABASE location_db TO darkvelocity;

-- Menu Service Database
CREATE DATABASE menu_db;
GRANT ALL PRIVILEGES ON DATABASE menu_db TO darkvelocity;

-- Orders Service Database
CREATE DATABASE orders_db;
GRANT ALL PRIVILEGES ON DATABASE orders_db TO darkvelocity;

-- Payments Service Database
CREATE DATABASE payments_db;
GRANT ALL PRIVILEGES ON DATABASE payments_db TO darkvelocity;

-- Hardware Service Database
CREATE DATABASE hardware_db;
GRANT ALL PRIVILEGES ON DATABASE hardware_db TO darkvelocity;

-- Inventory Service Database (Stock, Recipes, FIFO batches)
CREATE DATABASE inventory_db;
GRANT ALL PRIVILEGES ON DATABASE inventory_db TO darkvelocity;

-- Procurement Service Database (Suppliers, POs, Deliveries)
CREATE DATABASE procurement_db;
GRANT ALL PRIVILEGES ON DATABASE procurement_db TO darkvelocity;

-- Costing Service Database (Recipe costs, margins)
CREATE DATABASE costing_db;
GRANT ALL PRIVILEGES ON DATABASE costing_db TO darkvelocity;

-- Reporting Service Database (Projections, analytics)
CREATE DATABASE reporting_db;
GRANT ALL PRIVILEGES ON DATABASE reporting_db TO darkvelocity;

-- SpiceDB Database (Relationship-based authorization)
CREATE DATABASE spicedb;
GRANT ALL PRIVILEGES ON DATABASE spicedb TO darkvelocity;

-- Connect to each database and enable UUID extension
\c auth_db
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

\c location_db
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

\c menu_db
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

\c orders_db
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

\c payments_db
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

\c hardware_db
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

\c inventory_db
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

\c procurement_db
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

\c costing_db
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

\c reporting_db
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
