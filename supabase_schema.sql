-- supabase_schema.sql
-- Step 1: Database Schema

-- Enable UUID extension if not already enabled
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

CREATE TABLE Users (
    Id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    Name TEXT NOT NULL,
    Email TEXT UNIQUE NOT NULL,
    SystemRole TEXT CHECK (SystemRole IN ('Admin', 'Staff', 'Technician')) DEFAULT 'Staff',
    CreatedAt TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE RfidTags (
    Id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    TagString TEXT UNIQUE NOT NULL,
    UserId UUID REFERENCES Users(Id),
    Status TEXT CHECK (Status IN ('Active', 'Lost')) DEFAULT 'Active'
);

CREATE TABLE Sessions (
    Id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    RfidTagId UUID REFERENCES RfidTags(Id),
    StartTime TIMESTAMPTZ DEFAULT NOW(),
    EndTime TIMESTAMPTZ,
    Status TEXT CHECK (Status IN ('Active', 'Completed')) DEFAULT 'Active',
    TotalDurationMinutes INT,
    Fee DECIMAL(10,2)
);

-- Constraint: Ensure an RfidTag can only have one Active session at a time
CREATE UNIQUE INDEX idx_unique_active_session ON Sessions(RfidTagId) WHERE Status = 'Active';

-- Enable Row Level Security
ALTER TABLE Users ENABLE ROW LEVEL SECURITY;
ALTER TABLE RfidTags ENABLE ROW LEVEL SECURITY;
ALTER TABLE Sessions ENABLE ROW LEVEL SECURITY;

-- RLS Policies allowing a Service_Role to read/write to these tables
CREATE POLICY "ServiceRole Full Access Users" ON Users
    FOR ALL USING (auth.role() = 'service_role');

CREATE POLICY "ServiceRole Full Access RfidTags" ON RfidTags
    FOR ALL USING (auth.role() = 'service_role');

CREATE POLICY "ServiceRole Full Access Sessions" ON Sessions
    FOR ALL USING (auth.role() = 'service_role');

-- DEV-7 Machine Monitoring Schema
CREATE TABLE ArcadeMachines (
    Id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    Name TEXT NOT NULL,
    MachineType TEXT NOT NULL,
    Status TEXT CHECK (Status IN ('Online', 'Offline', 'InUse', 'Maintenance')) DEFAULT 'Online'
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

ALTER TABLE ArcadeMachines ENABLE ROW LEVEL SECURITY;
ALTER TABLE MachineUsageLogs ENABLE ROW LEVEL SECURITY;

CREATE POLICY "ServiceRole Full Access ArcadeMachines" ON ArcadeMachines
    FOR ALL USING (auth.role() = 'service_role');

CREATE POLICY "ServiceRole Full Access MachineUsageLogs" ON MachineUsageLogs
    FOR ALL USING (auth.role() = 'service_role');
<<<<<<< Updated upstream
=======

-- DEV-13 Manager Audit Logs Schema
CREATE TABLE ManagerAuditLogs (
    Id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    ManagerId UUID REFERENCES Users(Id),
    ManagerName TEXT NOT NULL,
    Action TEXT NOT NULL,
    Details TEXT,
    Timestamp TIMESTAMPTZ DEFAULT NOW()
);

ALTER TABLE ManagerAuditLogs ENABLE ROW LEVEL SECURITY;

CREATE POLICY "ServiceRole Full Access ManagerAuditLogs" ON ManagerAuditLogs
    FOR ALL USING (auth.role() = 'service_role');

-- DEV-8: Add guest and machine tracking to Sessions
ALTER TABLE Sessions ADD COLUMN GuestName TEXT DEFAULT 'Walk-in Guest';
ALTER TABLE Sessions ADD COLUMN MachineId UUID REFERENCES ArcadeMachines(Id);

-- DEV-8: Digital Receipts
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

ALTER TABLE DigitalReceipts ENABLE ROW LEVEL SECURITY;

CREATE POLICY "ServiceRole Full Access DigitalReceipts" ON DigitalReceipts
    FOR ALL USING (auth.role() = 'service_role');
>>>>>>> Stashed changes
