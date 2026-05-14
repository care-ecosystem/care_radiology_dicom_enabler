# CARE Radiology DICOM Enabler

## Project Overview

The CARE Radiology DICOM Enabler (formerly Plexus DICOM Enabler) is a comprehensive Windows-based healthcare imaging integration platform built on .NET Framework 4.7.2. It provides a complete DICOM (Digital Imaging and Communications in Medicine) networking solution that bridges medical imaging modalities (CT, MRI, X-Ray machines) with healthcare information systems.

**Built with:** Visual Studio 2019

## Purpose

This enterprise-grade solution enables:
- **DICOM Communication** between medical imaging devices and healthcare IT systems
- **Modality Worklist Services (MWL SCP)** for delivering scheduled patient information to imaging equipment
- **Image Reception (Store SCP)** for receiving DICOM images from modalities
- **Image Upload (Store SCU)** for transmitting images to remote DICOM servers
- **Multi-Server Management** with database-driven configuration
- **User Authentication** with periodic credential validation

## Architecture

### Solution Structure

The solution (`CARE_DICOM_Enabler.sln`) consists of 10 projects:

#### Core Application
- **Plexus_DICOM_Enabler** - Main WinForms application with Material Design UI
  - Entry point: `Program.cs:18`
  - Main form: `frm_Mainform.cs`
  - Login screen: `Forms/frm_LoginScreen.cs`

#### Windows Services
1. **CARE_Auth_Service** (`Plexus_Auth_Service`)
   - Authenticates user credentials periodically (every 24 hours)
   - Stops other services if authentication fails
   - Controls: MWL SCP, Store SCP, and Store SCU services

2. **CARE_MWL_Service** (`Plexus_MWL_Service`)
   - Modality Worklist SCP (Service Class Provider)
   - Provides patient lists to modalities via DICOM C-FIND
   - Supports three data sources: local list, MySQL database, remote CARE API
   - Default: AE Title "MODALITYSCP", Host 127.0.0.1, Port 2008

3. **CARE_StoreSCP_Service** (`Plexus_StoreSCP_Service`)
   - Store SCP service for receiving DICOM images
   - Implements DICOM C-STORE operations
   - Stores images locally in archive folders
   - Default: AE Title "STORAGESCP", Host 127.0.0.1, Port 2007
   - Implementation: `Network/CStoreSCP.cs`

4. **CARE_SCU_Service** (`Plexus_StoreSCU_Service`)
   - Store SCU (Service Class User) for uploading images
   - Periodically uploads received images to configured remote servers
   - Reads server list from MySQL database

#### Libraries & Utilities
- **CARE.DAL** (`Plexus.Common`)
  - Data Access Layer for MySQL operations
  - Encryption/decryption utilities (`EnDcryption.cs`)
  - Configuration management
  - User validation (`cls_UserDetail.cs`)
  - File: `ucls_DAL.cs`

- **GenerateConnectionString**
  - Utility for generating encrypted database connection strings
  - Tests database connectivity before saving
  - Saves to `config/common.cfg` (XML format)
  - Includes encryption/decryption tools

#### Unit Test Applications
- **Sample_ModalitySCP** - Tests Modality SCP functionality
- **Sample_Store_SCP** - Tests Store SCP and SCU operations
- **Test_SeriLog** - Tests SeriLog logging library
- **Plexus_FileDeleteApp** - Console app for deleting archive folders and files

## Technology Stack

### Frameworks
- **.NET Framework 4.7.2**
- **Windows Forms** with **MaterialSkin 2.3.1** (Material Design theme)

### DICOM Networking
- **fo-dicom 5.0.2** - Fellow Oak DICOM library (primary DICOM framework)
- **fo-dicom.Codecs 5.1.0** - Codec support for image compression
- Supported transfer syntaxes: JPEG-LS, JPEG2000, RLE, JPEG, uncompressed

### Database
- **MySql.Data 8.0.29** - MySQL .NET driver
- Connection string: `Server=localhost;Database=plexus_mi2;Uid=root;Pwd=<password>;`
- Encrypted storage in XML configuration files

### Logging
- **Serilog 2.11.0** - Structured logging framework
- **Serilog.Sinks.File 5.0.0** - File logging
- **Serilog.Sinks.Console 4.0.1** - Console logging

### Security & Encryption
- **BouncyCastle.Crypto 1.8.5** - Cryptography library
- Custom encryption for sensitive data (connection strings, credentials)

### Other Dependencies
- **Google.Protobuf 3.19.4** - Protocol Buffer serialization
- **System.Text.Json 6.0.5** - JSON serialization
- **K4os.Compression.LZ4 1.2.6** - Compression
- **Microsoft.Bcl.AsyncInterfaces 6.0.0** - Async utilities
- **System.Threading.Tasks.Extensions 4.5.4** - Threading extensions

## Configuration Files

### App.config (Main Application)
Located at: `App.config`

Key settings:
```xml
<appSettings>
  <!-- Modality Worklist SCP Settings -->
  <add key="mwlaetitle" value="MODALITYSCP" />
  <add key="mwlhost" value="127.0.0.1" />
  <add key="mwlport" value="2008" />

  <!-- Store SCP Settings -->
  <add key="sscpaetitle" value="STORAGESCP" />
  <add key="sscphost" value="127.0.0.1" />
  <add key="sscpport" value="2007" />

  <!-- Database Connection -->
  <add key="connectionstring" value="Server=localhost;Database=plexus_mi2;Uid=root;Pwd=inzin@123;" />

  <!-- Authentication API -->
  <add key="authURL" value="https://dev.plexusemr.com:8443/plx-api/users/login-api" />

  <!-- Deployment Type: 1=Client (Hospital), 2=Server (Central) -->
  <add key="deployType" value="1" />

  <add key="deviceName" value="Plexus" />
  <add key="checkserver" value="true" />
</appSettings>
```

### config/common.cfg
- Encrypted XML file containing database connection strings
- Generated using GenerateConnectionString utility
- Read by services using `cls_PlexusConfig.cs:` in StoreSCP service

## Main Application Features

The WinForms UI (`frm_Mainform.cs`) provides tabbed interface with:

1. **Server Manager**
   - Install/Uninstall Windows services
   - Start/Stop backend services
   - Control: `UserControls/uctrl_ServerManager.cs`

2. **SCP Settings**
   - Configure Modality SCP settings
   - Configure Store SCP settings
   - Control: `UserControls/uctrl_SCPSettings.cs`

3. **SCU Settings**
   - Configure Store SCU upload details
   - Set destination servers and timing

4. **Server List**
   - Add/Edit/Delete DICOM server endpoints
   - Database-driven server configuration
   - Manage multiple DICOM nodes for communication

5. **View Patient List**
   - Display patient information
   - Shows DICOM transactions (MWL/SCP/Upload)

6. **View Logs**
   - View DICOM communication logs
   - Separate logs for MWL, Store SCP, and Store SCU operations

7. **About Us**
   - Application information and version details

## DICOM Operations Supported

- **C-FIND** - Modality Worklist queries
- **C-STORE** - Image reception and transmission
- **C-ECHO** - Service verification/ping

## Deployment Model

### Deployment Types (deployType setting)
1. **Client Mode (deployType=1)** - Deployed at hospitals/clinics
   - Receives images from local modalities
   - Uploads to central servers

2. **Server Mode (deployType=2)** - Deployed at central locations
   - Acts as central image archive
   - Receives from multiple client installations

### Installation Steps
1. Build solution in Visual Studio 2019
2. Install NuGet packages (see below)
3. Configure database connection using GenerateConnectionString utility
4. Update `App.config` with appropriate AE titles, hosts, and ports
5. Install Windows services using service installers
6. Configure DICOM servers in Server List
7. Start services via Server Manager UI

## Required NuGet Packages

```
fo-dicom 5.0.2
fo-dicom.Codecs 5.1.0
Microsoft.Bcl.AsyncInterfaces 6.0.0
Serilog 2.11.0
Serilog.Sinks.File 5.0.0
System.Buffers 4.5.1
System.Numerics.Vectors 4.5.0
System.Runtime.CompilerServices.Unsafe 6.0.0
System.Text.Encodings.Web 6.0.0
System.Text.Json 6.0.5
System.Threading.Tasks.Extension 4.5.4
System.ValueTuple 4.5.0
MySql.Data 8.0.29
MaterialSkin 2.3.1
BouncyCastle.Crypto 1.8.5
Google.Protobuf 3.19.4
K4os.Compression.LZ4 1.2.6
```

## Directory Structure

```
/
├── CARE.DAL/                      # Data Access Layer library
├── CARE_Auth_Service/             # Authentication Windows Service
├── CARE_MWL_Service/             # Modality Worklist SCP Service
├── CARE_SCU_Service/             # Store SCU Upload Service
├── CARE_StoreSCP_Service/        # Store SCP Reception Service
│   └── Network/
│       └── CStoreSCP.cs          # DICOM Store provider implementation
├── Forms/                         # Application forms
│   └── frm_LoginScreen.cs        # Login screen
├── UserControls/                  # Reusable UI controls
│   ├── uctrl_LoginForm.cs
│   ├── uctrl_SCPSettings.cs
│   └── uctrl_ServerManager.cs
├── GenerateConnectionString/      # Connection string utility
├── Sample_ModalitySCP/           # MWL SCP unit tests
├── Sample_Store_SCP/             # Store SCP/SCU unit tests
├── Test_SeriLog/                 # SeriLog unit tests
├── config/                        # Configuration files
│   └── common.cfg                # Encrypted connection string
├── Images/                        # UI images and icons
├── bin/                          # Build outputs
├── obj/                          # Intermediate objects
├── Program.cs                    # Application entry point
├── frm_Mainform.cs              # Main application form
├── Global.cs                     # Global variables
├── App.config                    # Application configuration
├── packages.config               # NuGet package manifest
├── CARE_DICOM_Enabler.csproj    # Main project file
├── CARE_DICOM_Enabler.sln       # Visual Studio solution
└── README.md                     # Project documentation
```

## Key Classes and Files

### Entry Points
- `Program.cs:18` - Application Main() method
- Sets `Global._applicationPath` from `Application.ExecutablePath`
- Launches `frm_LoginScreen` for authentication

### Authentication Flow
1. User enters credentials in `frm_LoginScreen`
2. Validates against stored credentials (database or config)
3. On success, opens `frm_Mainform`
4. `CARE_Auth_Service` periodically validates (every 24 hours)
5. Stops services if authentication fails

### DICOM Image Flow
1. **Reception**: Modality sends C-STORE → `CARE_StoreSCP_Service` receives → Stores locally
2. **Upload**: `CARE_SCU_Service` monitors archive → Uploads to configured servers → Marks as completed

### Configuration Management
- `CARE.DAL/ucls_DAL.cs` - Database operations
- `CARE.DAL/EnDcryption.cs` - Encrypt/decrypt sensitive data
- `CARE_StoreSCP_Service/config/cls_PlexusConfig.cs` - Read XML config

## Development Guidelines

### Building the Solution
1. Open `CARE_DICOM_Enabler.sln` in Visual Studio 2019
2. Restore NuGet packages
3. Build each project individually or entire solution
4. Output binaries in `/bin/Debug` or `/bin/Release`

### Testing
- Use unit test applications in solution
- `Sample_ModalitySCP` - Test MWL queries
- `Sample_Store_SCP` - Test C-STORE operations
- Validate with actual DICOM modalities or simulators

### Security Considerations
- Database credentials encrypted using BouncyCastle
- Connection strings stored in encrypted XML format
- User authentication required for UI access
- Service-level authentication with periodic validation
- Sensitive data never logged in plaintext

### Logging
- Structured logs via Serilog
- File-based logging with rotation
- Separate logs for each service
- View logs via "View Logs" tab in UI

## Target Users

- Hospital IT administrators
- Radiology department staff
- Healthcare system integrators
- DICOM network operators
- Medical imaging technicians

## Integration Points

### Data Sources (Modality Worklist)
1. Local patient list
2. MySQL database tables
3. Remote CARE system API (`authURL` configuration)

### DICOM Nodes
- Configurable via Server List UI
- Stored in MySQL database
- Multiple servers supported
- Each with AE Title, Host, Port, Description

### External Systems
- Remote CARE EMR system for authentication
- Central PACS/VNA systems for image archival
- RIS systems for worklist data

## Troubleshooting

### Common Issues
1. **Service won't start**: Check Windows Event Log, verify ports not in use
2. **Database connection fails**: Use GenerateConnectionString utility to test
3. **DICOM communication errors**: Verify AE titles, hosts, ports match on both ends
4. **Authentication fails**: Check authURL and network connectivity
5. **Images not uploading**: Verify server list configuration and network access

### Log Locations
- Check application directory for Serilog output files
- Windows Event Viewer for service errors
- View Logs tab in UI for DICOM transaction logs

## License & Credits

Originally developed as Plexus DICOM Enabler, now maintained as CARE Radiology DICOM Enabler.

Built with fo-dicom (Fellow Oak DICOM) - industry-standard .NET DICOM library.

---

For questions or issues, refer to the README.md file or contact the development team.
