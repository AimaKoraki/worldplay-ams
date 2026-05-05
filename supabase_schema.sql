-- supabase_schema.sql
-- Worldplay AMS Database Schema

-- Enable UUID extension if not already enabled
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ============================================
-- Core Entities
-- ============================================

CREATE TABLE Users (
    Id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    Name TEXT NOT NULL,
    Email TEXT UNIQUE NOT NULL,
    SystemRole TEXT CHECK (SystemRole IN ('Admin', 'Staff', 'Technician')) DEFAULT 'Staff',
    FirstName TEXT,
    LastName TEXT,
    CreatedAt TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE Customers (
    Id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    FirstName TEXT NOT NULL,
    LastName TEXT NOT NULL,
    Email TEXT,
    PhoneNumber TEXT,
    DOB DATE,
    Type TEXT CHECK (Type IN ('Regular', 'VIP', 'Minor')) DEFAULT 'Regular',
    GuardianId UUID REFERENCES Customers(Id),
    RegistrationDate TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE Zones (
    Id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    ZoneName TEXT NOT NULL,
    Description TEXT
);

CREATE TABLE RfidTags (
    Id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    TagString TEXT UNIQUE NOT NULL,
    UserId UUID REFERENCES Users(Id),
    Status TEXT CHECK (Status IN ('Active', 'Lost')) DEFAULT 'Active',
    IssueDate TIMESTAMPTZ DEFAULT NOW(),
    LastUsedDate TIMESTAMPTZ
);

-- ============================================
-- Machine & Zone Management
-- ============================================

CREATE TABLE ArcadeMachines (
    Id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    Name TEXT NOT NULL,
    MachineType TEXT NOT NULL,
    Status TEXT CHECK (Status IN ('Online', 'Offline', 'InUse', 'Maintenance')) DEFAULT 'Online',
    Category TEXT,
    InstallationDate DATE,
    LastServiceDate DATE,
    CurrentCostPerPlay DECIMAL(10,2),
    ZoneId UUID REFERENCES Zones(Id)
);

CREATE TABLE MachineUsageLogs (
    Id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    MachineId UUID REFERENCES ArcadeMachines(Id),
    StartTime TIMESTAMPTZ DEFAULT NOW(),
    EndTime TIMESTAMPTZ,
    DurationMinutes INT,
    Status TEXT CHECK (Status IN ('Active', 'Completed')) DEFAULT 'Active'
);

-- Constraint: Ensure an ArcadeMachine can only have one Active log at a time
CREATE UNIQUE INDEX idx_unique_active_machine_log ON MachineUsageLogs(MachineId) WHERE Status = 'Active';

-- ============================================
-- Sessions & Gameplay
-- ============================================

CREATE TABLE Sessions (
    Id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    RfidTagId UUID REFERENCES RfidTags(Id),
    StartTime TIMESTAMPTZ DEFAULT NOW(),
    EndTime TIMESTAMPTZ,
    Status TEXT CHECK (Status IN ('Active', 'Completed')) DEFAULT 'Active',
    TotalDurationMinutes INT,
    Fee DECIMAL(10,2),
    GuestName TEXT DEFAULT 'Walk-in Guest',
    MachineId UUID REFERENCES ArcadeMachines(Id),
    CheckedOutByStaff TEXT,
    CustomerId UUID REFERENCES Customers(Id)
);

-- Constraint: Ensure an RfidTag can only have one Active session at a time
CREATE UNIQUE INDEX idx_unique_active_session ON Sessions(RfidTagId) WHERE Status = 'Active';

-- ============================================
-- Transactions & Payments
-- ============================================

CREATE TABLE Transactions (
    Id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    SessionId UUID REFERENCES Sessions(Id),
    CustomerId UUID REFERENCES Customers(Id),
    Amount DECIMAL(10,2) NOT NULL,
    PaymentMethod TEXT CHECK (PaymentMethod IN ('Cash', 'Card', 'Digital', 'Complimentary')) DEFAULT 'Cash',
    Status TEXT CHECK (Status IN ('Completed', 'Pending', 'Refunded')) DEFAULT 'Completed',
    Timestamp TIMESTAMPTZ DEFAULT NOW()
);

-- ============================================
-- Audit & Receipts
-- ============================================

CREATE TABLE ManagerAuditLogs (
    Id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    ManagerId UUID REFERENCES Users(Id),
    ManagerName TEXT NOT NULL,
    Action TEXT NOT NULL,
    Details TEXT,
    Timestamp TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE DigitalReceipts (
    Id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    SessionId UUID REFERENCES Sessions(Id) NOT NULL UNIQUE,
    ReceiptNumber TEXT UNIQUE NOT NULL,
    RfidTagId UUID REFERENCES RfidTags(Id) NOT NULL,
    GuestName TEXT NOT NULL DEFAULT 'Walk-in Guest',
    MachineName TEXT,
    CheckInTime TIMESTAMPTZ NOT NULL,
    CheckOutTime TIMESTAMPTZ NOT NULL,
    DurationMinutes INT NOT NULL,
    Fee DECIMAL(10,2) NOT NULL,
    StaffName TEXT NOT NULL,
    IssuedAt TIMESTAMPTZ DEFAULT NOW(),
    Status TEXT CHECK (Status IN ('Issued', 'Voided')) DEFAULT 'Issued'
);

CREATE UNIQUE INDEX idx_unique_receipt_session ON DigitalReceipts(SessionId);

-- ============================================
-- Row Level Security
-- ============================================

ALTER TABLE Users ENABLE ROW LEVEL SECURITY;
ALTER TABLE Customers ENABLE ROW LEVEL SECURITY;
ALTER TABLE Zones ENABLE ROW LEVEL SECURITY;
ALTER TABLE RfidTags ENABLE ROW LEVEL SECURITY;
ALTER TABLE ArcadeMachines ENABLE ROW LEVEL SECURITY;
ALTER TABLE MachineUsageLogs ENABLE ROW LEVEL SECURITY;
ALTER TABLE Sessions ENABLE ROW LEVEL SECURITY;
ALTER TABLE Transactions ENABLE ROW LEVEL SECURITY;
ALTER TABLE ManagerAuditLogs ENABLE ROW LEVEL SECURITY;
ALTER TABLE DigitalReceipts ENABLE ROW LEVEL SECURITY;

-- Service Role Full Access Policies
CREATE POLICY "ServiceRole Full Access Users" ON Users
    FOR ALL USING (auth.role() = 'service_role');

CREATE POLICY "ServiceRole Full Access Customers" ON Customers
    FOR ALL USING (auth.role() = 'service_role');

CREATE POLICY "ServiceRole Full Access Zones" ON Zones
    FOR ALL USING (auth.role() = 'service_role');

CREATE POLICY "ServiceRole Full Access RfidTags" ON RfidTags
    FOR ALL USING (auth.role() = 'service_role');

CREATE POLICY "ServiceRole Full Access ArcadeMachines" ON ArcadeMachines
    FOR ALL USING (auth.role() = 'service_role');

CREATE POLICY "ServiceRole Full Access MachineUsageLogs" ON MachineUsageLogs
    FOR ALL USING (auth.role() = 'service_role');

CREATE POLICY "ServiceRole Full Access Sessions" ON Sessions
    FOR ALL USING (auth.role() = 'service_role');

CREATE POLICY "ServiceRole Full Access Transactions" ON Transactions
    FOR ALL USING (auth.role() = 'service_role');

CREATE POLICY "ServiceRole Full Access ManagerAuditLogs" ON ManagerAuditLogs
    FOR ALL USING (auth.role() = 'service_role');

CREATE POLICY "ServiceRole Full Access DigitalReceipts" ON DigitalReceipts
    FOR ALL USING (auth.role() = 'service_role');
