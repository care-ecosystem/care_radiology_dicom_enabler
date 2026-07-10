# Cross-Platform Migration Guide
## From Windows .NET Framework to Platform-Agnostic DICOM Enabler

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Current Architecture Analysis](#current-architecture-analysis)
3. [Platform Dependencies and Limitations](#platform-dependencies-and-limitations)
4. [Cross-Platform Technology Stack Options](#cross-platform-technology-stack-options)
5. [Recommended Architecture](#recommended-architecture)
6. [Implementation Roadmap](#implementation-roadmap)
7. [Migration Strategy](#migration-strategy)
8. [Deployment Options](#deployment-options)
9. [Performance Considerations](#performance-considerations)
10. [Security and Compliance](#security-and-compliance)
11. [Cost-Benefit Analysis](#cost-benefit-analysis)
12. [Getting Started](#getting-started)

---

## Executive Summary

### Current State

The CARE Radiology DICOM Enabler is currently built on **Windows-specific technologies**:
- **.NET Framework 4.7.2** (Windows-only, legacy framework)
- **Windows Forms** (Windows-only GUI framework)
- **Windows Services** (Windows-only background service model)
- **MySQL .NET Driver** (works but optimized for Windows)
- **BouncyCastle .NET** (Windows-focused cryptography)

### Problem Statement

This architecture creates several limitations:

1. **Platform Lock-in**: Cannot run on Linux, macOS, or containerized environments
2. **Deployment Complexity**: Requires Windows Server licenses, VMs, or bare metal
3. **Scalability Issues**: Cannot leverage Kubernetes, Docker Swarm for orchestration
4. **Cloud Limitations**: Difficult to deploy on AWS ECS, Google Cloud Run, Azure Container Instances
5. **Maintenance Burden**: .NET Framework is in maintenance mode (no new features)
6. **Integration Challenges**: Harder to integrate with modern cloud-native CARE backend
7. **Development Constraints**: Requires Windows development machines

### Proposed Solution

Create a **platform-agnostic DICOM enabler** that:
- ✅ Runs on Windows, Linux, macOS, Docker, Kubernetes
- ✅ Uses modern cross-platform frameworks (.NET 8+, Python, Go, or Node.js)
- ✅ Eliminates GUI dependency (use REST API + Web UI instead)
- ✅ Supports containerization and cloud deployment
- ✅ Integrates seamlessly with Django CARE backend
- ✅ Reduces licensing costs (no Windows Server needed)
- ✅ Enables horizontal scaling and high availability

### Expected Benefits

| Benefit | Impact |
|---------|--------|
| **Platform Independence** | Deploy anywhere - on-premises, cloud, hybrid |
| **Container Support** | Docker, Kubernetes orchestration |
| **Cost Reduction** | Eliminate Windows Server licenses (~$1,000+/server) |
| **Scalability** | Horizontal scaling with load balancers |
| **Cloud-Native** | First-class support for AWS, GCP, Azure |
| **Developer Productivity** | Develop on any OS, use modern tooling |
| **Maintenance** | Active frameworks with long-term support |
| **Integration** | Native REST APIs, microservices architecture |

---

## Current Architecture Analysis

### Component Breakdown

#### 1. CARE_MWL_Service (Modality Worklist SCP)

**Current Implementation:**
```csharp
// Windows Service using fo-dicom
public class PlexusMWLService : ServiceBase
{
    private IDicomServer _dicomServer;

    protected override void OnStart(string[] args)
    {
        _dicomServer = DicomServerFactory.Create<MwlSCP>(2008);
    }
}

// C-FIND SCP Handler
public class MwlSCP : DicomService, IDicomServiceProvider, IDicomCFindProvider
{
    public Task OnReceiveAssociationRequestAsync(DicomAssociation association)
    {
        // Accept association
        // Handle C-FIND request
        // Query MySQL or HTTP API
        // Return DICOM worklist items
    }
}
```

**Platform-Specific Dependencies:**
- `System.ServiceProcess.ServiceBase` - Windows Services API
- Windows Registry for configuration
- Windows Event Log for logging
- COM interop for some operations

**Required Functionality:**
- DICOM C-FIND SCP implementation
- HTTP client to query Django worklist API
- Static API key authentication
- DICOM dataset serialization
- Multi-threaded request handling

---

#### 2. CARE_StoreSCP_Service (Image Receiver)

**Current Implementation:**
```csharp
public class PlexusStoreSCPService : ServiceBase
{
    protected override void OnStart(string[] args)
    {
        var server = DicomServerFactory.Create<CStoreSCP>(2007);
    }
}

public class CStoreSCP : DicomService, IDicomServiceProvider, IDicomCStoreProvider
{
    public async Task OnCStoreRequestAsync(DicomCStoreRequest request)
    {
        var dataset = request.Dataset;
        var studyUID = dataset.GetString(DicomTag.StudyInstanceUID);

        // Save to filesystem: ./SCP/{StudyUID}/{SeriesUID}/{InstanceUID}.dcm
        var filePath = Path.Combine(storagePath, studyUID, seriesUID, $"{instanceUID}.dcm");
        await File.WriteAllBytesAsync(filePath, dicomFile.ToBytes());

        // Update MySQL database
        await _dal.InsertStudyAsync(studyUID, patientID, ...);

        return DicomStatus.Success;
    }
}
```

**Platform-Specific Dependencies:**
- Windows file paths (`C:\`, backslashes)
- Windows Service lifecycle
- NTFS permissions and ACLs
- Windows file watching (FileSystemWatcher)

**Required Functionality:**
- DICOM C-STORE SCP implementation
- File storage with directory structure
- Database updates (study/series/instance metadata)
- Transfer syntax negotiation
- Association management
- Validation of calling AE titles

---

#### 3. CARE_SCU_Service (Image Uploader)

**Current Implementation:**
```csharp
public class Plexus_SCU_Service : ServiceBase
{
    private System.Timers.Timer _uploadTimer;

    protected override void OnStart(string[] args)
    {
        _uploadTimer = new System.Timers.Timer(5000); // 5 seconds
        _uploadTimer.Elapsed += OnElapsedTime;
        _uploadTimer.Start();
    }

    private async void OnElapsedTime(object source, ElapsedEventArgs e)
    {
        // Get pending files from database
        var pendingInstances = await _dal.GetPendingUploadsAsync();

        foreach (var instance in pendingInstances)
        {
            // Read DICOM file
            var dicomFile = await DicomFile.OpenAsync(instance.FilePath);

            // Upload via DICOM C-STORE
            var client = DicomClientFactory.Create(serverHost, serverPort, false, callingAE, calledAE);
            await client.AddRequestAsync(new DicomCStoreRequest(dicomFile));
            await client.SendAsync();

            // OR upload via HTTP POST to Django
            var httpClient = new HttpClient();
            var content = new MultipartFormDataContent();
            content.Add(new ByteArrayContent(dicomFile.ToBytes()), "file", $"{instanceUID}.dcm");
            var response = await httpClient.PostAsync($"{djangoUrl}/api/plugin/care_radiology/dicom/upload/", content);

            if (response.IsSuccessStatusCode)
            {
                // Mark as uploaded, delete local file
                await _dal.UpdateUploadStatusAsync(instance.Id, "success");
                File.Delete(instance.FilePath);
            }
        }
    }
}
```

**Platform-Specific Dependencies:**
- `System.Timers.Timer` (works cross-platform but service model doesn't)
- Windows Service hosting
- Windows file locking mechanisms

**Required Functionality:**
- Periodic job scheduler
- Directory monitoring for new files
- DICOM C-STORE SCU (client) implementation
- HTTP multipart/form-data upload
- JWT token management
- Retry logic with exponential backoff
- File cleanup after successful upload

---

#### 4. CARE_Auth_Service (Authentication Validator)

**Current Implementation:**
```csharp
public class PlexusAuthService : ServiceBase
{
    private System.Timers.Timer _authTimer;

    protected override void OnStart(string[] args)
    {
        _authTimer = new System.Timers.Timer(86400000); // 24 hours
        _authTimer.Elapsed += ValidateCredentials;
        _authTimer.Start();
    }

    private async void ValidateCredentials(object source, ElapsedEventArgs e)
    {
        var httpClient = new HttpClient();
        var response = await httpClient.PostAsync(authURL, credentials);

        if (!response.IsSuccessStatusCode)
        {
            // Stop other services
            ServiceController mwlService = new ServiceController("CARE MWL Service");
            mwlService.Stop();

            ServiceController scpService = new ServiceController("CARE StoreSCP Service");
            scpService.Stop();

            ServiceController scuService = new ServiceController("CARE StoreSCU Service");
            scuService.Stop();
        }
    }
}
```

**Platform-Specific Dependencies:**
- `System.ServiceProcess.ServiceController` (Windows-only API)
- Windows Service management
- Windows security context

**Required Functionality:**
- Periodic authentication check (24 hour interval)
- HTTP client for API calls
- Service lifecycle management (start/stop other services)
- Secure credential storage

---

#### 5. WinForms GUI Application

**Current Implementation:**
```csharp
public partial class frm_Mainform : MaterialForm
{
    private void btnStartServices_Click(object sender, EventArgs e)
    {
        ServiceController mwlService = new ServiceController("CARE MWL Service");
        mwlService.Start();
        // ...
    }

    private void btnServerList_Click(object sender, EventArgs e)
    {
        // CRUD operations on MySQL servers table
        dgvServers.DataSource = _dal.GetServers();
    }
}
```

**Platform-Specific Dependencies:**
- Windows Forms (Windows-only GUI)
- MaterialSkin library (Windows-only theme)
- Windows-specific controls and dialogs
- GDI+ rendering engine

**Required Functionality:**
- Service management UI (start/stop/install/uninstall)
- Configuration management (SCP/SCU settings)
- Server list CRUD
- Patient list viewer
- Log viewer
- User authentication
- About/version information

---

#### 6. Data Access Layer (CARE.DAL)

**Current Implementation:**
```csharp
public class ucls_DAL
{
    private MySqlConnection _connection;

    public ucls_DAL(string connectionString)
    {
        _connection = new MySqlConnection(connectionString);
    }

    public async Task<List<Study>> GetPendingUploadsAsync()
    {
        var command = new MySqlCommand("SELECT * FROM instance WHERE upload_status = 'pending'", _connection);
        // ...
    }
}

public class EnDcryption
{
    public static string Encrypt(string plainText)
    {
        // BouncyCastle encryption
        var cipher = CipherUtilities.GetCipher("AES/CBC/PKCS7Padding");
        // ...
    }
}
```

**Platform-Specific Dependencies:**
- `MySql.Data` (works cross-platform but some Windows optimizations)
- BouncyCastle .NET version (cross-platform but .NET Framework specific)
- Windows DPAPI for secure storage (optional, but used)

**Required Functionality:**
- MySQL connection management
- CRUD operations for study/series/instance/servers
- Connection string encryption/decryption
- Transaction management
- Connection pooling

---

### Summary of Platform Dependencies

| Component | Windows Dependencies | Impact |
|-----------|---------------------|---------|
| **MWL Service** | ServiceBase, Event Log | High - Cannot run on Linux |
| **StoreSCP Service** | ServiceBase, NTFS paths | High - Service model + file system |
| **StoreSCU Service** | ServiceBase, ServiceController | High - Service lifecycle management |
| **Auth Service** | ServiceController, Service API | Critical - Cannot manage services cross-platform |
| **WinForms GUI** | Windows Forms, MaterialSkin, GDI+ | Critical - 100% Windows-only |
| **Data Layer** | MySql.Data (.NET FW), BouncyCastle | Low - Mostly portable |
| **DICOM Library** | fo-dicom 5.0.2 | Low - Has .NET 6+ version |
| **Logging** | Serilog | None - Fully cross-platform |
| **Configuration** | App.config, Registry | Medium - XML format but Windows-specific APIs |

**Overall Assessment**: ~80% of codebase has Windows dependencies that require rewrite or abstraction.

---

## Platform Dependencies and Limitations

### 1. .NET Framework 4.7.2 Limitations

**.NET Framework (4.x) vs .NET (6+)**:

| Feature | .NET Framework 4.7.2 | .NET 8 (Cross-Platform) |
|---------|----------------------|-------------------------|
| **Platforms** | Windows only | Windows, Linux, macOS, Docker |
| **Performance** | Baseline | 2-5x faster (in many scenarios) |
| **Memory** | Higher GC pressure | Optimized GC, lower memory |
| **Runtime** | CLR (Windows) | CoreCLR (cross-platform) |
| **APIs** | Windows-specific APIs | Platform-agnostic APIs |
| **Deployment** | Requires .NET FW install | Self-contained or runtime-dependent |
| **AOT Compilation** | No | Yes (Native AOT in .NET 8) |
| **Containers** | Poor support | First-class Docker support |
| **Future** | Maintenance mode | Active development, LTS |
| **ARM Support** | No | Yes (ARM64 on Linux/macOS) |

**Migration Path**: .NET Framework → .NET 8 (LTS until Nov 2026)

---

### 2. Windows Services vs Cross-Platform Services

**Windows Service Model**:
```csharp
// Windows-specific
public class MyService : ServiceBase
{
    protected override void OnStart(string[] args) { }
    protected override void OnStop() { }
}

// Installation requires:
sc.exe create "ServiceName" binPath="C:\path\to\service.exe"
```

**Cross-Platform Alternatives**:

**Option A: .NET Generic Host (Recommended)**
```csharp
// Works on Windows, Linux, macOS
public class Program
{
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseSystemd() // Linux systemd support
            .UseWindowsService() // Windows Service support
            .ConfigureServices((hostContext, services) =>
            {
                services.AddHostedService<MwlWorker>();
                services.AddHostedService<StoreSCPWorker>();
                services.AddHostedService<StoreSCUWorker>();
            });
}

public class MwlWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // DICOM MWL SCP logic here
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }
}
```

**Option B: systemd (Linux)**
```ini
# /etc/systemd/system/care-mwl.service
[Unit]
Description=CARE Modality Worklist SCP
After=network.target

[Service]
Type=notify
ExecStart=/usr/local/bin/care-mwl-service
Restart=always
User=careuser
Environment=ASPNETCORE_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```

**Option C: Docker Container (Universal)**
```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:8.0
COPY ./app /app
WORKDIR /app
ENTRYPOINT ["dotnet", "CareMwlService.dll"]
```

**Option D: Kubernetes Deployment**
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: care-mwl-service
spec:
  replicas: 3
  template:
    spec:
      containers:
      - name: mwl
        image: care/mwl-service:latest
        ports:
        - containerPort: 2008
```

---

### 3. GUI Dependency Analysis

**Current WinForms GUI Issues**:
- Cannot run headless on servers
- Requires RDP/remote desktop for access
- No multi-user support
- Difficult to automate
- Poor accessibility
- No mobile access
- No API-first design

**Cross-Platform GUI Alternatives**:

| Option | Technology | Pros | Cons |
|--------|-----------|------|------|
| **Web UI** | React + Django REST | Universal access, mobile-friendly, API-driven | Requires web server |
| **Electron** | JavaScript + Node.js | Cross-platform desktop, modern UI | Heavy (~200MB) |
| **Avalonia** | C# + XAML | Native .NET, similar to WPF | Smaller ecosystem |
| **MAUI** | C# + XAML | Microsoft-backed, native iOS/Android | Still maturing |
| **REST API Only** | OpenAPI + Swagger | Language-agnostic, automation-friendly | No built-in UI |
| **CLI** | Command-line | Lightweight, scriptable | Not user-friendly |

**Recommended**: **REST API + Web UI** for maximum flexibility and integration with existing Django CARE frontend.

---

### 4. File System Dependencies

**Current Issues**:
```csharp
// Windows-specific path
var path = @"C:\SCP\Study123\Series456\Instance789.dcm";

// Windows path separator
var parts = path.Split('\\');

// Windows drives
var drive = Path.GetPathRoot("C:\\SCP");
```

**Cross-Platform Solution**:
```csharp
// Use Path.Combine (handles separators automatically)
var path = Path.Combine(storagePath, studyUID, seriesUID, $"{instanceUID}.dcm");

// Use forward slashes or Path.DirectorySeparatorChar
var separator = Path.DirectorySeparatorChar; // \ on Windows, / on Linux

// Use relative paths or environment variables
var storagePath = Environment.GetEnvironmentVariable("DICOM_STORAGE_PATH") ?? "/var/lib/care/dicom";
```

**Storage Options**:
- **Local filesystem**: Works cross-platform with proper path handling
- **S3-compatible storage**: MinIO, AWS S3, Google Cloud Storage
- **Network shares**: NFS (Linux), SMB/CIFS (Windows/Linux)
- **Object storage**: Better for cloud deployments

---

### 5. Database Considerations

**Current MySQL Usage**:
```csharp
var connectionString = "Server=localhost;Database=plexus_mi2;Uid=root;Pwd=password;";
var connection = new MySqlConnection(connectionString);
```

**Cross-Platform Database Options**:

| Database | Cross-Platform | Docker | Cloud-Native | Best For |
|----------|---------------|---------|--------------|----------|
| **PostgreSQL** | ✅ | ✅ | ✅ (AWS RDS, etc.) | Recommended (used by Django) |
| **MySQL** | ✅ | ✅ | ✅ (AWS RDS, etc.) | Current choice |
| **SQLite** | ✅ | ✅ | ❌ | Development only |
| **SQL Server** | ✅ | ✅ | ✅ (Azure SQL) | Microsoft ecosystem |

**Recommendation**: **Migrate to PostgreSQL** for consistency with Django CARE backend, or keep MySQL but use cross-platform driver.

---

## Cross-Platform Technology Stack Options

### Option 1: .NET 8 (Recommended for C# Developers)

**Overview**: Modernize existing .NET Framework codebase to .NET 8 with minimal changes.

**Technology Stack**:
```
Runtime:      .NET 8 (LTS until Nov 2026)
DICOM:        fo-dicom 5.1.0+ (.NET 6+ compatible)
Database:     Npgsql (PostgreSQL) or MySqlConnector
Web:          ASP.NET Core (for REST API)
Background:   Generic Host + BackgroundService
Logging:      Serilog (already used, cross-platform)
Config:       appsettings.json + Environment Variables
Containers:   Official Microsoft .NET Docker images
```

**Migration Complexity**: **Low** (60-70% code reusable with modifications)

**Pros**:
✅ Familiar language and ecosystem
✅ Excellent performance (comparable to Go)
✅ Strong typing and async/await
✅ fo-dicom library has .NET 6+ support
✅ Easy migration path from .NET Framework
✅ Native AOT compilation available
✅ Microsoft long-term support

**Cons**:
❌ Larger runtime size (~80MB vs 10MB for Go)
❌ Higher memory usage than Go/Rust
❌ Less common in cloud-native DevOps tooling

**Sample Architecture**:
```
care-dicom-enabler/
├── src/
│   ├── CareDicom.Core/              # Shared domain logic
│   ├── CareDicom.MwlService/        # Worklist SCP
│   ├── CareDicom.StoreScpService/   # Storage SCP
│   ├── CareDicom.StoreScuService/   # Upload SCU
│   ├── CareDicom.WebApi/            # REST API + Web UI
│   └── CareDicom.Shared/            # Common utilities
├── docker/
│   ├── Dockerfile.mwl
│   ├── Dockerfile.storescp
│   ├── Dockerfile.storescu
│   └── docker-compose.yml
├── k8s/
│   ├── deployment.yaml
│   └── service.yaml
└── tests/
```

**Code Example**:
```csharp
// MWL Service as .NET Generic Host
public class MwlWorker : BackgroundService
{
    private readonly ILogger<MwlWorker> _logger;
    private readonly IConfiguration _config;
    private IDicomServer _server;

    public MwlWorker(ILogger<MwlWorker> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var port = _config.GetValue<int>("DicomMwl:Port");
        _server = DicomServerFactory.Create<MwlSCP>(port);

        _logger.LogInformation($"MWL SCP started on port {port}");

        // Keep running until cancellation
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MWL SCP stopping");
        _server?.Dispose();
        await base.StopAsync(cancellationToken);
    }
}

// Startup configuration
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<MwlWorker>();
builder.Services.AddSingleton<IDicomWorklistProvider, DjangoWorklistProvider>();

var app = builder.Build();
await app.RunAsync();
```

---

### Option 2: Python (Best Integration with Django)

**Overview**: Rewrite in Python for seamless integration with Django CARE backend.

**Technology Stack**:
```
Runtime:      Python 3.11+ (or PyPy for performance)
DICOM:        pynetdicom (pure Python DICOM implementation)
Database:     psycopg3 (PostgreSQL) or PyMySQL
Web:          FastAPI or Django REST Framework
Background:   asyncio + APScheduler
Logging:      structlog or Python logging
Config:       pydantic-settings + environment variables
Containers:   python:3.11-slim Docker images
```

**Migration Complexity**: **High** (complete rewrite required)

**Pros**:
✅ Native integration with Django CARE backend
✅ Share models, authentication, database with Django
✅ Rich DICOM ecosystem (pydicom, pynetdicom)
✅ Excellent async support (asyncio)
✅ Easy to deploy and maintain
✅ Large talent pool
✅ Could be integrated as Django app itself

**Cons**:
❌ Slower performance than .NET/Go (but acceptable for DICOM)
❌ GIL limitations for CPU-bound tasks
❌ Higher memory usage than Go
❌ Complete rewrite required

**Sample Architecture**:
```
care-dicom-enabler/
├── care_dicom_enabler/
│   ├── __init__.py
│   ├── mwl_scp.py                  # Worklist SCP using pynetdicom
│   ├── store_scp.py                # Storage SCP
│   ├── store_scu.py                # Upload SCU
│   ├── api/
│   │   ├── __init__.py
│   │   └── views.py                # REST API endpoints
│   ├── models.py                   # Database models (if separate DB)
│   └── config.py                   # Configuration
├── docker/
│   └── Dockerfile
├── pyproject.toml
└── tests/
```

**Code Example**:
```python
# MWL SCP using pynetdicom
from pynetdicom import AE, evt
from pynetdicom.sop_class import ModalityWorklistInformationFind
import httpx
import os

def handle_find(event):
    """Handle C-FIND request for worklist"""
    dataset = event.identifier

    # Query Django CARE backend
    api_url = os.getenv("CARE_API_URL")
    api_key = os.getenv("CARE_API_KEY")

    async with httpx.AsyncClient() as client:
        response = await client.get(
            f"{api_url}/api/plugin/care_radiology/dicom/worklist/",
            headers={"Authorization": api_key}
        )

    worklist_items = response.json()

    # Yield each matching worklist item as DICOM dataset
    for item in worklist_items['results']:
        ds = Dataset()
        ds.PatientName = item['patient']['name']
        ds.PatientID = item['patient']['id']
        # ... populate other tags

        yield (0xFF00, ds)  # Pending status with dataset

# Create Application Entity
ae = AE(ae_title=os.getenv("MWL_AE_TITLE", "MODALITYSCP"))
ae.add_supported_context(ModalityWorklistInformationFind)

# Start SCP server
handlers = [(evt.EVT_C_FIND, handle_find)]
ae.start_server(("0.0.0.0", 2008), evt_handlers=handlers)
```

**Django Integration Option**:
Could be implemented as a Django management command:
```python
# care/management/commands/run_mwl_scp.py
from django.core.management.base import BaseCommand
from care_radiology.dicom.mwl_scp import start_mwl_server

class Command(BaseCommand):
    help = 'Run Modality Worklist SCP server'

    def handle(self, *args, **options):
        start_mwl_server()
```

---

### Option 3: Go (Best Performance & Cloud-Native)

**Overview**: Rewrite in Go for maximum performance and cloud-native deployment.

**Technology Stack**:
```
Runtime:      Go 1.22+
DICOM:        go-netdicom (Google's DICOM library)
Database:     pgx (PostgreSQL) or go-sql-driver/mysql
Web:          Gin or Echo (lightweight REST frameworks)
Background:   Goroutines + channels
Logging:      zap or zerolog (high-performance)
Config:       viper + environment variables
Containers:   golang:alpine or scratch (tiny images)
```

**Migration Complexity**: **Very High** (complete rewrite, different paradigm)

**Pros**:
✅ Excellent performance (compiled, no GC pauses)
✅ Tiny memory footprint (~20-50MB vs 100MB+ for .NET)
✅ Native concurrency (goroutines)
✅ Fast compilation and startup
✅ Single binary deployment (no runtime needed)
✅ Tiny Docker images (5-15MB with scratch base)
✅ First-class Kubernetes support
✅ Cloud-native ecosystem (Docker, K8s tools often in Go)

**Cons**:
❌ Complete rewrite required (different language)
❌ Less mature DICOM library (go-netdicom)
❌ Learning curve if team not familiar with Go
❌ Fewer healthcare libraries compared to Python

**Sample Architecture**:
```
care-dicom-enabler/
├── cmd/
│   ├── mwl-scp/main.go
│   ├── store-scp/main.go
│   ├── store-scu/main.go
│   └── api/main.go
├── pkg/
│   ├── dicom/                      # DICOM utilities
│   ├── storage/                    # File/S3 storage
│   ├── database/                   # Database access
│   └── config/                     # Configuration
├── internal/                       # Private application logic
├── docker/
│   └── Dockerfile
└── go.mod
```

**Code Example**:
```go
// MWL SCP using go-netdicom
package main

import (
    "context"
    "github.com/grailbio/go-netdicom"
    "github.com/grailbio/go-netdicom/dimse"
    "net/http"
)

func handleCFind(ctx context.Context, req *dimse.CFindRequest) ([]*dimse.CFindResponse, error) {
    // Query Django CARE backend
    apiURL := os.Getenv("CARE_API_URL")
    apiKey := os.Getenv("CARE_API_KEY")

    client := &http.Client{}
    httpReq, _ := http.NewRequest("GET", apiURL + "/api/plugin/care_radiology/dicom/worklist/", nil)
    httpReq.Header.Set("Authorization", apiKey)

    resp, err := client.Do(httpReq)
    if err != nil {
        return nil, err
    }

    var worklist WorklistResponse
    json.NewDecoder(resp.Body).Decode(&worklist)

    // Convert to DICOM responses
    var responses []*dimse.CFindResponse
    for _, item := range worklist.Results {
        ds := createDicomDataset(item)
        responses = append(responses, &dimse.CFindResponse{
            Status: dimse.StatusPending,
            Dataset: ds,
        })
    }

    return responses, nil
}

func main() {
    ae := netdicom.NewApplicationEntity("MODALITYSCP")
    ae.AddService(netdicom.Service{
        SOPClass: netdicom.ModalityWorklistInformationFind,
        Handler: netdicom.CFindHandler(handleCFind),
    })

    ae.ListenAndServe(":2008")
}
```

---

### Option 4: Node.js (JavaScript Ecosystem)

**Overview**: Use Node.js for JavaScript developers familiar with web technologies.

**Technology Stack**:
```
Runtime:      Node.js 20 LTS
DICOM:        dcmjs-dimse (DICOM networking) + dcmjs (parsing)
Database:     pg (PostgreSQL) or mysql2
Web:          Express.js or Fastify
Background:   node-cron + worker_threads
Logging:      winston or pino
Config:       dotenv + config
Containers:   node:20-alpine Docker images
```

**Migration Complexity**: **Very High** (complete rewrite, different paradigm)

**Pros**:
✅ Familiar to web developers
✅ Huge npm ecosystem
✅ Native async/await
✅ Good integration with modern web frontends
✅ Active healthcare JS community (OHIF, etc.)

**Cons**:
❌ Less mature DICOM libraries
❌ Single-threaded (need worker_threads for concurrency)
❌ Higher memory usage
❌ Performance concerns for high-throughput scenarios

**Code Example**:
```javascript
// MWL SCP using dcmjs-dimse
const { Server } = require('dcmjs-dimse');
const axios = require('axios');

const server = new Server({
  aet: process.env.MWL_AE_TITLE || 'MODALITYSCP',
  port: 2008,
});

server.on('cFindRequest', async (association, message) => {
  const dataset = message.dataset;

  // Query Django CARE backend
  const response = await axios.get(
    `${process.env.CARE_API_URL}/api/plugin/care_radiology/dicom/worklist/`,
    {
      headers: { Authorization: process.env.CARE_API_KEY }
    }
  );

  const worklistItems = response.data.results;

  // Send each worklist item as C-FIND response
  for (const item of worklistItems) {
    const responseDataset = createDicomDataset(item);
    association.sendCFindResponse(message.messageId, responseDataset, 0xFF00);
  }

  // Send final success response
  association.sendCFindResponse(message.messageId, null, 0x0000);
});

server.listen();
```

---

## Recommended Architecture

### Hybrid Approach: .NET 8 Backend + React Web UI

**Why This Combination**:

1. **Minimal Migration Effort**: 60-70% of existing C# code reusable
2. **Performance**: Excellent for DICOM operations (comparable to Go)
3. **Ecosystem**: fo-dicom library is mature and actively maintained
4. **Team Skills**: Leverages existing .NET knowledge
5. **Modern Stack**: .NET 8 is truly cross-platform and cloud-native
6. **Web UI**: React integrates with existing CARE frontend
7. **Deployment**: First-class Docker and Kubernetes support

---

### System Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ Medical Imaging Devices (CT, MRI, X-Ray, Ultrasound)                        │
└────────────────────────────┬────────────────────────────────────────────────┘
                             │
                    DICOM C-FIND (2008)  │  DICOM C-STORE (2007)
                             │                        │
                             ↓                        ↓
┌────────────────────────────────────────────────────────────────────────────┐
│ CARE DICOM Enabler (Platform-Agnostic)                                     │
├────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌──────────────────────┐  ┌──────────────────────┐  ┌──────────────────┐│
│  │ MWL Service (.NET 8) │  │ StoreSCP (.NET 8)    │  │ StoreSCU (.NET 8)││
│  │ Port: 2008           │  │ Port: 2007           │  │ Background Worker││
│  │ ─────────────────────│  │ ─────────────────────│  │ ─────────────────││
│  │ • C-FIND SCP         │  │ • C-STORE SCP        │  │ • Periodic Upload││
│  │ • Query Django API   │  │ • Save to Storage    │  │ • DICOM C-STORE  ││
│  │ • Return Worklist    │  │ • Update PostgreSQL  │  │ • HTTP POST      ││
│  └──────────────────────┘  └──────────────────────┘  └──────────────────┘│
│                                                                             │
│  ┌───────────────────────────────────────────────────────────────────────┐│
│  │ Management API (ASP.NET Core)                                         ││
│  │ Port: 5000 (HTTP) / 5001 (HTTPS)                                     ││
│  │ ─────────────────────────────────────────────────────────────────────││
│  │ • Service status and control                                          ││
│  │ • Configuration management (servers, settings)                        ││
│  │ • Patient list queries                                                ││
│  │ • Log aggregation and viewing                                         ││
│  │ • Health checks and metrics (Prometheus)                              ││
│  │ • OpenAPI/Swagger documentation                                       ││
│  └───────────────────────────────────────────────────────────────────────┘│
│                                                                             │
│  ┌───────────────────────────────────────────────────────────────────────┐│
│  │ Shared Storage Layer                                                  ││
│  │ ─────────────────────────────────────────────────────────────────────││
│  │ • Local filesystem: /var/lib/care/dicom                              ││
│  │ • S3-compatible: MinIO, AWS S3, GCS                                   ││
│  │ • Network share: NFS, SMB/CIFS                                        ││
│  └───────────────────────────────────────────────────────────────────────┘│
│                                                                             │
│  ┌───────────────────────────────────────────────────────────────────────┐│
│  │ Database: PostgreSQL                                                  ││
│  │ ─────────────────────────────────────────────────────────────────────││
│  │ • study, series, instance tables                                      ││
│  │ • servers configuration                                                ││
│  │ • upload_queue for retry logic                                        ││
│  └───────────────────────────────────────────────────────────────────────┘│
│                                                                             │
└─────────────────────┬───────────────────────────────────────────────────────┘
                      │
                      │ HTTP/HTTPS REST API
                      ↓
┌─────────────────────────────────────────────────────────────────────────────┐
│ Web UI (React + TypeScript)                                                 │
├─────────────────────────────────────────────────────────────────────────────┤
│ • Service dashboard (start/stop/restart)                                    │
│ • Configuration UI (server list, SCP/SCU settings)                          │
│ • Patient list viewer with search/filter                                    │
│ • Log viewer with real-time updates (WebSocket)                             │
│ • Metrics and monitoring (charts via Recharts)                              │
│ • Responsive design (desktop, tablet, mobile)                               │
│ • Dark mode support                                                         │
└─────────────────────────────────────────────────────────────────────────────┘
                      │
                      │ HTTP REST API
                      ↓
┌─────────────────────────────────────────────────────────────────────────────┐
│ Django CARE Backend                                                         │
├─────────────────────────────────────────────────────────────────────────────┤
│ • Worklist API (GET /api/plugin/care_radiology/dicom/worklist/)            │
│ • Upload API (POST /api/plugin/care_radiology/dicom/upload/)               │
│ • Webhook receiver (POST /api/plugin/care_radiology/webhooks/study/)       │
│ • Study Report CRUD                                                         │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

### Component Specifications

#### 1. MWL Service (Modality Worklist SCP)

**Technology**: .NET 8 + fo-dicom + Generic Host

**Responsibilities**:
- Listen on configurable port (default: 2008)
- Accept DICOM associations from imaging devices
- Handle C-FIND requests
- Query Django worklist API via HTTP
- Convert JSON response to DICOM datasets
- Return worklist items to device

**Configuration** (appsettings.json):
```json
{
  "DicomMwl": {
    "AeTitle": "MODALITYSCP",
    "Port": 2008,
    "MaxPduLength": 131072,
    "Timeout": 30
  },
  "CareBackend": {
    "WorklistUrl": "http://django:9000/api/plugin/care_radiology/dicom/worklist/",
    "ApiKey": "static-api-key-from-env"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "FellowOakDicom": "Warning"
    }
  }
}
```

**Deployment**:
- Docker container: `care/mwl-service:latest`
- Kubernetes Deployment with 2-3 replicas
- Service type: NodePort or LoadBalancer exposing port 2008
- Resource limits: 256MB RAM, 0.5 CPU

---

#### 2. StoreSCP Service (Image Receiver)

**Technology**: .NET 8 + fo-dicom + Generic Host

**Responsibilities**:
- Listen on configurable port (default: 2007)
- Accept DICOM associations from imaging devices
- Receive C-STORE requests with DICOM images
- Validate calling AE title (configurable whitelist)
- Save DICOM files to storage (filesystem or S3)
- Extract metadata and update PostgreSQL
- Support multiple transfer syntaxes
- Log all transactions

**Configuration**:
```json
{
  "DicomStoreScp": {
    "AeTitle": "STORAGESCP",
    "Port": 2007,
    "AllowedCallingAeTitles": ["*"],  // Or specific list
    "MaxPduLength": 262144,
    "AcceptedTransferSyntaxes": [
      "1.2.840.10008.1.2",      // Implicit VR Little Endian
      "1.2.840.10008.1.2.1",    // Explicit VR Little Endian
      "1.2.840.10008.1.2.4.70", // JPEG Lossless
      "1.2.840.10008.1.2.5"     // RLE Lossless
    ]
  },
  "Storage": {
    "Type": "FileSystem",  // or "S3"
    "BasePath": "/var/lib/care/dicom",
    "S3Bucket": "dicom-bucket",
    "S3Endpoint": "http://minio:9000"
  },
  "Database": {
    "ConnectionString": "Host=postgres;Database=dicom;Username=postgres;Password=postgres"
  }
}
```

**Storage Structure**:
```
/var/lib/care/dicom/
├── {StudyInstanceUID}/
│   ├── {SeriesInstanceUID}/
│   │   ├── {SOPInstanceUID}.dcm
│   │   ├── {SOPInstanceUID}.dcm
│   │   └── ...
│   └── {SeriesInstanceUID}/
└── {StudyInstanceUID}/
```

**Database Tables**:
```sql
CREATE TABLE study (
    id SERIAL PRIMARY KEY,
    study_uid VARCHAR(500) UNIQUE NOT NULL,
    patient_id VARCHAR(100),
    study_date DATE,
    study_time TIME,
    modality_codes VARCHAR(100),
    num_instances INT DEFAULT 0,
    storage_path VARCHAR(1000),
    received_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_study_uid (study_uid),
    INDEX idx_patient_id (patient_id)
);

CREATE TABLE series (
    id SERIAL PRIMARY KEY,
    series_uid VARCHAR(500) UNIQUE NOT NULL,
    study_id INT REFERENCES study(id) ON DELETE CASCADE,
    modality VARCHAR(20),
    series_number INT,
    num_instances INT DEFAULT 0,
    INDEX idx_series_uid (series_uid)
);

CREATE TABLE instance (
    id SERIAL PRIMARY KEY,
    sop_instance_uid VARCHAR(500) UNIQUE NOT NULL,
    series_id INT REFERENCES series(id) ON DELETE CASCADE,
    instance_number INT,
    file_path VARCHAR(1000),
    file_size BIGINT,
    transfer_syntax VARCHAR(100),
    upload_status VARCHAR(20) DEFAULT 'pending',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    uploaded_at TIMESTAMP,
    INDEX idx_sop_instance_uid (sop_instance_uid),
    INDEX idx_upload_status (upload_status)
);
```

**Deployment**:
- Docker container with volume mounts
- Stateful deployment (needs persistent storage)
- Resource limits: 512MB RAM, 1 CPU
- Health check: C-ECHO on port 2007

---

#### 3. StoreSCU Service (Image Uploader)

**Technology**: .NET 8 + fo-dicom + Background Worker + Quartz.NET

**Responsibilities**:
- Periodic job execution (configurable interval, default: 5 seconds)
- Query database for pending uploads
- Read DICOM files from storage
- Upload via DICOM C-STORE to configured servers OR
- Upload via HTTP POST to Django `/dicom/upload/` endpoint
- Handle JWT token refresh
- Retry logic with exponential backoff
- Update database upload status
- Delete local files after successful upload (configurable)

**Configuration**:
```json
{
  "DicomStoreScu": {
    "CallingAeTitle": "CARESTORESCUU",
    "UploadInterval": 5,  // seconds
    "BatchSize": 10,      // files per batch
    "MaxRetries": 3,
    "RetryDelay": 30,     // seconds
    "DeleteAfterUpload": true
  },
  "UploadDestinations": [
    {
      "Type": "Dicom",
      "Name": "DCM4CHEE",
      "CalledAeTitle": "DCM4CHEE",
      "Host": "dcm4chee",
      "Port": 11112,
      "Enabled": true
    },
    {
      "Type": "Http",
      "Name": "Django Backend",
      "Url": "http://django:9000/api/plugin/care_radiology/dicom/upload/",
      "AuthType": "Bearer",
      "JwtTokenUrl": "http://django:9000/api/token/",
      "Username": "dicom_user",
      "Password": "secret",
      "Enabled": true
    }
  ]
}
```

**Upload Workflow**:
```
1. Query: SELECT * FROM instance WHERE upload_status = 'pending' LIMIT {BatchSize}
2. For each instance:
   a. Read DICOM file from storage
   b. Try upload to each enabled destination
   c. If success:
      - UPDATE instance SET upload_status = 'success', uploaded_at = NOW()
      - DELETE file if DeleteAfterUpload = true
   d. If failure:
      - Increment retry_count
      - If retry_count < MaxRetries:
          UPDATE instance SET upload_status = 'retrying', next_retry_at = NOW() + RetryDelay
      - Else:
          UPDATE instance SET upload_status = 'failed'
3. Sleep for UploadInterval
4. Repeat
```

**Deployment**:
- Docker container (stateless, needs storage volume)
- Kubernetes CronJob or Deployment (single replica recommended)
- Resource limits: 256MB RAM, 0.5 CPU

---

#### 4. Management API (ASP.NET Core Web API)

**Technology**: ASP.NET Core 8 + Entity Framework Core + OpenAPI

**Responsibilities**:
- RESTful API for service management
- Configuration CRUD (servers, settings)
- Patient list queries with pagination/filtering
- Log aggregation and search
- Health checks (liveness, readiness)
- Metrics export (Prometheus format)
- WebSocket endpoint for real-time log streaming
- JWT authentication (shared with Django)

**API Endpoints**:

```yaml
openapi: 3.0.0
info:
  title: CARE DICOM Enabler API
  version: 1.0.0

paths:
  /api/services:
    get:
      summary: List all services
      responses:
        '200':
          content:
            application/json:
              schema:
                type: array
                items:
                  type: object
                  properties:
                    name: string
                    status: string  # running, stopped, error
                    uptime: string

  /api/services/{serviceName}/start:
    post:
      summary: Start a service

  /api/services/{serviceName}/stop:
    post:
      summary: Stop a service

  /api/services/{serviceName}/restart:
    post:
      summary: Restart a service

  /api/servers:
    get:
      summary: List configured DICOM servers
    post:
      summary: Add new server

  /api/servers/{id}:
    get:
      summary: Get server details
    put:
      summary: Update server
    delete:
      summary: Delete server

  /api/patients:
    get:
      summary: List patients with DICOM studies
      parameters:
        - name: search
          in: query
        - name: page
        - name: page_size

  /api/studies:
    get:
      summary: List DICOM studies
      parameters:
        - name: patient_id
        - name: study_date_from
        - name: study_date_to

  /api/logs:
    get:
      summary: Query logs
      parameters:
        - name: level
        - name: service
        - name: start_time
        - name: end_time

  /api/logs/stream:
    get:
      summary: WebSocket endpoint for real-time logs

  /api/health:
    get:
      summary: Health check

  /api/metrics:
    get:
      summary: Prometheus metrics
```

**Configuration**:
```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:5000"
      },
      "Https": {
        "Url": "https://0.0.0.0:5001",
        "Certificate": {
          "Path": "/certs/cert.pfx",
          "Password": "certpassword"
        }
      }
    }
  },
  "Authentication": {
    "JwtSecret": "shared-secret-with-django",
    "JwtIssuer": "care-dicom-enabler",
    "JwtAudience": "care-api"
  },
  "Cors": {
    "AllowedOrigins": ["http://localhost:3000", "https://care.hospital.org"]
  }
}
```

**Deployment**:
- Docker container exposing ports 5000 (HTTP) and 5001 (HTTPS)
- Kubernetes Service (ClusterIP) + Ingress
- Resource limits: 512MB RAM, 1 CPU

---

#### 5. Web UI (React + TypeScript)

**Technology**: React 18 + TypeScript + Vite + TailwindCSS + Recharts

**Features**:
- Service dashboard with status cards
- Start/Stop/Restart buttons with real-time status
- Configuration forms for servers and settings
- Patient list table with search, filter, pagination
- Study viewer (open in OHIF)
- Log viewer with search, filter, auto-refresh
- Real-time log streaming via WebSocket
- Metrics dashboard with charts
- Responsive design (mobile, tablet, desktop)
- Dark mode toggle
- User authentication (JWT)

**Project Structure**:
```
web-ui/
├── src/
│   ├── components/
│   │   ├── ServiceDashboard.tsx
│   │   ├── ServiceCard.tsx
│   │   ├── ServerList.tsx
│   │   ├── ServerForm.tsx
│   │   ├── PatientList.tsx
│   │   ├── LogViewer.tsx
│   │   └── MetricsDashboard.tsx
│   ├── pages/
│   │   ├── Dashboard.tsx
│   │   ├── Configuration.tsx
│   │   ├── Patients.tsx
│   │   └── Logs.tsx
│   ├── services/
│   │   ├── api.ts          # Axios client
│   │   └── websocket.ts    # WebSocket client
│   ├── hooks/
│   │   ├── useServices.ts
│   │   ├── useServers.ts
│   │   └── useLogs.ts
│   ├── App.tsx
│   └── main.tsx
├── package.json
└── vite.config.ts
```

**Sample Component**:
```typescript
// ServiceDashboard.tsx
import { useState, useEffect } from 'react';
import { ServiceCard } from './ServiceCard';
import { useServices } from '../hooks/useServices';

export const ServiceDashboard = () => {
  const { services, loading, startService, stopService, restartService } = useServices();

  return (
    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4 p-6">
      {services.map(service => (
        <ServiceCard
          key={service.name}
          service={service}
          onStart={() => startService(service.name)}
          onStop={() => stopService(service.name)}
          onRestart={() => restartService(service.name)}
        />
      ))}
    </div>
  );
};
```

**Deployment**:
- Static build served by nginx
- Docker container with nginx:alpine
- Kubernetes Deployment + Service + Ingress
- CDN distribution (CloudFront, CloudFlare)

---

### Containerization Strategy

**Docker Compose (Development)**:
```yaml
version: '3.9'

services:
  postgres:
    image: postgres:15-alpine
    environment:
      POSTGRES_DB: dicom
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
    ports:
      - "5432:5432"
    volumes:
      - postgres-data:/var/lib/postgresql/data

  mwl-service:
    build:
      context: .
      dockerfile: docker/Dockerfile.mwl
    ports:
      - "2008:2008"
    environment:
      DicomMwl__Port: 2008
      CareBackend__WorklistUrl: http://django:9000/api/plugin/care_radiology/dicom/worklist/
      CareBackend__ApiKey: ${CARE_API_KEY}
    depends_on:
      - postgres

  storescp-service:
    build:
      context: .
      dockerfile: docker/Dockerfile.storescp
    ports:
      - "2007:2007"
    environment:
      DicomStoreScp__Port: 2007
      Storage__BasePath: /var/lib/care/dicom
      Database__ConnectionString: Host=postgres;Database=dicom;Username=postgres;Password=postgres
    volumes:
      - dicom-storage:/var/lib/care/dicom
    depends_on:
      - postgres

  storescu-service:
    build:
      context: .
      dockerfile: docker/Dockerfile.storescu
    environment:
      DicomStoreScu__UploadInterval: 5
      UploadDestinations__0__Url: http://django:9000/api/plugin/care_radiology/dicom/upload/
      Storage__BasePath: /var/lib/care/dicom
    volumes:
      - dicom-storage:/var/lib/care/dicom
    depends_on:
      - postgres
      - storescp-service

  api:
    build:
      context: .
      dockerfile: docker/Dockerfile.api
    ports:
      - "5000:5000"
    environment:
      Database__ConnectionString: Host=postgres;Database=dicom;Username=postgres;Password=postgres
      Authentication__JwtSecret: ${JWT_SECRET}
    depends_on:
      - postgres

  web-ui:
    build:
      context: ./web-ui
      dockerfile: Dockerfile
    ports:
      - "3001:80"
    environment:
      VITE_API_URL: http://localhost:5000

volumes:
  postgres-data:
  dicom-storage:
```

**Kubernetes Deployment (Production)**:
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: care-mwl-service
spec:
  replicas: 2
  selector:
    matchLabels:
      app: care-mwl-service
  template:
    metadata:
      labels:
        app: care-mwl-service
    spec:
      containers:
      - name: mwl
        image: care/mwl-service:v2.0.0
        ports:
        - containerPort: 2008
          protocol: TCP
        env:
        - name: DicomMwl__Port
          value: "2008"
        - name: CareBackend__ApiKey
          valueFrom:
            secretKeyRef:
              name: care-secrets
              key: api-key
        resources:
          requests:
            memory: "128Mi"
            cpu: "250m"
          limits:
            memory: "256Mi"
            cpu: "500m"
        livenessProbe:
          tcpSocket:
            port: 2008
          initialDelaySeconds: 10
          periodSeconds: 30
        readinessProbe:
          tcpSocket:
            port: 2008
          initialDelaySeconds: 5
          periodSeconds: 10

---
apiVersion: v1
kind: Service
metadata:
  name: care-mwl-service
spec:
  type: NodePort
  selector:
    app: care-mwl-service
  ports:
  - port: 2008
    targetPort: 2008
    nodePort: 32008
    protocol: TCP
```

---

## Implementation Roadmap

### Phase 1: Foundation (Weeks 1-4)

**Goal**: Set up cross-platform development environment and core infrastructure.

**Tasks**:
1. **Week 1: Project Setup**
   - [ ] Create new .NET 8 solution structure
   - [ ] Set up Git repository with branching strategy
   - [ ] Configure CI/CD pipeline (GitHub Actions or Azure DevOps)
   - [ ] Set up development containers (Dev Containers)
   - [ ] Create Docker Compose for local development
   - [ ] Set up PostgreSQL database schema
   - [ ] Configure logging (Serilog to structured JSON)

2. **Week 2: Shared Infrastructure**
   - [ ] Implement configuration management (appsettings.json + env vars)
   - [ ] Create database access layer with EF Core
   - [ ] Implement storage abstraction (filesystem + S3)
   - [ ] Set up health check infrastructure
   - [ ] Create metrics collection (Prometheus)
   - [ ] Implement JWT authentication library

3. **Week 3: DICOM Core**
   - [ ] Upgrade fo-dicom to latest .NET 8 compatible version
   - [ ] Create DICOM service base classes
   - [ ] Implement association management
   - [ ] Create DICOM dataset serialization utilities
   - [ ] Write unit tests for DICOM utilities

4. **Week 4: API Foundation**
   - [ ] Create ASP.NET Core Web API project
   - [ ] Implement authentication middleware
   - [ ] Set up Swagger/OpenAPI documentation
   - [ ] Create basic CRUD endpoints for servers
   - [ ] Write integration tests

**Deliverables**:
- Working development environment
- Database schema
- Core libraries and utilities
- Basic API with authentication

---

### Phase 2: MWL Service Migration (Weeks 5-7)

**Goal**: Implement cross-platform Modality Worklist SCP service.

**Tasks**:
1. **Week 5: Core Implementation**
   - [ ] Create MwlService project using Generic Host
   - [ ] Implement C-FIND SCP handler
   - [ ] Create Django HTTP client for worklist API
   - [ ] Implement JSON to DICOM dataset conversion
   - [ ] Add configurable AE title and port

2. **Week 6: Testing & Refinement**
   - [ ] Write unit tests for C-FIND handler
   - [ ] Test with dcmtk findscu tool
   - [ ] Test with real medical devices (if available)
   - [ ] Implement error handling and logging
   - [ ] Add metrics (requests/sec, latency)

3. **Week 7: Containerization**
   - [ ] Create Dockerfile for MWL service
   - [ ] Test Docker container locally
   - [ ] Create Kubernetes manifests
   - [ ] Deploy to test cluster
   - [ ] Load testing and optimization

**Deliverables**:
- Cross-platform MWL SCP service
- Docker image
- Kubernetes deployment
- Test results and documentation

---

### Phase 3: StoreSCP Service Migration (Weeks 8-10)

**Goal**: Implement cross-platform Storage SCP service.

**Tasks**:
1. **Week 8: Core Implementation**
   - [ ] Create StoreSCPService project using Generic Host
   - [ ] Implement C-STORE SCP handler
   - [ ] Create filesystem storage implementation
   - [ ] Create S3 storage implementation (abstract interface)
   - [ ] Implement database updates for study/series/instance

2. **Week 9: Transfer Syntax & Validation**
   - [ ] Implement multiple transfer syntax support
   - [ ] Add AE title validation (whitelist/blacklist)
   - [ ] Implement association validation
   - [ ] Add file integrity checks (DICOM validation)
   - [ ] Implement concurrent C-STORE handling

3. **Week 10: Testing & Optimization**
   - [ ] Test with dcmtk storescu tool
   - [ ] Test with real devices
   - [ ] Performance testing (concurrent connections)
   - [ ] Memory profiling and optimization
   - [ ] Create Docker image and K8s manifests

**Deliverables**:
- Cross-platform Storage SCP service
- Support for local and S3 storage
- Performance benchmarks
- Deployment artifacts

---

### Phase 4: StoreSCU Service Migration (Weeks 11-13)

**Goal**: Implement cross-platform upload service.

**Tasks**:
1. **Week 11: Core Implementation**
   - [ ] Create StoreSCUService as BackgroundService
   - [ ] Implement job scheduler (Quartz.NET)
   - [ ] Create database query for pending uploads
   - [ ] Implement DICOM C-STORE SCU client
   - [ ] Implement HTTP multipart upload client

2. **Week 12: Retry Logic & Resilience**
   - [ ] Implement retry logic with exponential backoff
   - [ ] Add circuit breaker for HTTP uploads (Polly)
   - [ ] Implement upload queue prioritization
   - [ ] Add batch upload optimization
   - [ ] Implement cleanup logic (delete after upload)

3. **Week 13: JWT & Testing**
   - [ ] Implement JWT token management and refresh
   - [ ] Test with Django backend
   - [ ] Test with DCM4CHEE
   - [ ] Test failure scenarios and recovery
   - [ ] Create monitoring dashboards

**Deliverables**:
- Cross-platform upload service
- Robust retry and error handling
- Deployment artifacts

---

### Phase 5: Management API (Weeks 14-16)

**Goal**: Create REST API for service management and monitoring.

**Tasks**:
1. **Week 14: Core Endpoints**
   - [ ] Implement service status endpoints
   - [ ] Implement service control endpoints (start/stop/restart)
   - [ ] Implement server CRUD endpoints
   - [ ] Implement patient list endpoints
   - [ ] Implement study query endpoints

2. **Week 15: Logs & Metrics**
   - [ ] Implement log query endpoint with filtering
   - [ ] Implement WebSocket endpoint for real-time logs
   - [ ] Implement metrics endpoint (Prometheus format)
   - [ ] Add health check endpoints
   - [ ] Create OpenAPI documentation

3. **Week 16: Testing & Documentation**
   - [ ] Write API integration tests
   - [ ] Test authentication and authorization
   - [ ] Create Postman collection
   - [ ] Write API documentation
   - [ ] Performance testing

**Deliverables**:
- Complete REST API
- WebSocket support for real-time updates
- API documentation
- Docker image

---

### Phase 6: Web UI (Weeks 17-20)

**Goal**: Create modern web-based management interface.

**Tasks**:
1. **Week 17: Setup & Core Pages**
   - [ ] Create React + TypeScript + Vite project
   - [ ] Set up routing (React Router)
   - [ ] Create layout components (sidebar, header)
   - [ ] Implement authentication (login page, JWT storage)
   - [ ] Create dashboard page with service cards

2. **Week 18: Configuration UI**
   - [ ] Create server list page with table
   - [ ] Implement server add/edit form with validation
   - [ ] Create settings page for SCP/SCU configuration
   - [ ] Implement form validation with Zod
   - [ ] Add confirmation dialogs for destructive actions

3. **Week 19: Monitoring & Logs**
   - [ ] Create patient list page with search/filter
   - [ ] Create log viewer with search and filters
   - [ ] Implement WebSocket integration for real-time logs
   - [ ] Create metrics dashboard with charts (Recharts)
   - [ ] Add auto-refresh for status updates

4. **Week 20: Polish & Testing**
   - [ ] Implement dark mode
   - [ ] Add responsive design (mobile, tablet)
   - [ ] Write component tests (Vitest)
   - [ ] Add loading states and error handling
   - [ ] User acceptance testing
   - [ ] Create Docker image with nginx

**Deliverables**:
- Modern web UI
- Responsive design
- Docker image
- User documentation

---

### Phase 7: Integration & Testing (Weeks 21-23)

**Goal**: End-to-end testing and integration with Django CARE backend.

**Tasks**:
1. **Week 21: Django Integration**
   - [ ] Test worklist API integration
   - [ ] Test upload API integration
   - [ ] Test webhook integration
   - [ ] Verify JWT authentication compatibility
   - [ ] Test with OHIF viewer

2. **Week 22: Device Testing**
   - [ ] Test with DICOM emulators (dcmtk, Orthanc)
   - [ ] Test with real medical devices (CT, MRI, X-Ray)
   - [ ] Test concurrent connections
   - [ ] Test large file uploads (>100MB)
   - [ ] Test various transfer syntaxes

3. **Week 23: Load Testing**
   - [ ] Performance testing with k6 or JMeter
   - [ ] Test 100+ concurrent associations
   - [ ] Test 1000+ image uploads
   - [ ] Memory profiling under load
   - [ ] Identify and fix bottlenecks

**Deliverables**:
- Comprehensive test results
- Performance benchmarks
- Bug fixes and optimizations

---

### Phase 8: Deployment & Migration (Weeks 24-26)

**Goal**: Deploy to production and migrate from old Windows system.

**Tasks**:
1. **Week 24: Production Setup**
   - [ ] Create Helm charts for Kubernetes
   - [ ] Set up production databases (HA PostgreSQL)
   - [ ] Configure production storage (S3/NFS)
   - [ ] Set up monitoring (Prometheus + Grafana)
   - [ ] Set up log aggregation (ELK or Loki)
   - [ ] Configure SSL/TLS certificates

2. **Week 25: Data Migration**
   - [ ] Create migration scripts for MySQL → PostgreSQL
   - [ ] Migrate study/series/instance metadata
   - [ ] Migrate server configurations
   - [ ] Copy DICOM files to new storage
   - [ ] Verify data integrity

3. **Week 26: Cutover & Validation**
   - [ ] Deploy to production
   - [ ] Update device configurations (AE titles, IPs)
   - [ ] Run parallel with old system (1 week)
   - [ ] Monitor for issues
   - [ ] Decommission old Windows system
   - [ ] Documentation and training

**Deliverables**:
- Production deployment
- Migrated data
- Monitoring dashboards
- Runbooks and documentation

---

### Total Timeline: 26 weeks (~6 months)

**Resource Requirements**:
- 2-3 full-time developers
- 1 DevOps engineer
- 1 QA tester
- 1 DICOM integration specialist (part-time)

---

## Migration Strategy

### Parallel Operation Approach (Recommended)

Run old Windows system and new cross-platform system in parallel during transition period.

**Setup**:
```
Old Windows System:
- MWL SCP on 192.168.1.100:2008 (AE: MODALITYSCP_OLD)
- Store SCP on 192.168.1.100:2007 (AE: STORAGESCP_OLD)

New Cross-Platform System:
- MWL SCP on 192.168.1.101:2008 (AE: MODALITYSCP_NEW)
- Store SCP on 192.168.1.101:2007 (AE: STORAGESCP_NEW)
```

**Phased Rollout**:

1. **Week 1-2: Pilot Testing (10% of devices)**
   - Configure 1-2 test devices to use new system
   - Monitor for issues
   - Validate worklist queries and image uploads
   - Compare with old system results

2. **Week 3-4: Early Adopters (30% of devices)**
   - Migrate non-critical modalities (e.g., ultrasound)
   - Continue monitoring
   - Gather feedback from technicians

3. **Week 5-6: Majority Rollout (80% of devices)**
   - Migrate most devices
   - Keep critical devices on old system as backup
   - 24/7 monitoring and support

4. **Week 7-8: Complete Migration (100%)**
   - Migrate remaining devices
   - Decommission old Windows system
   - Archive logs and data

**Rollback Plan**:
- Keep old system online for 2 weeks after complete migration
- Document rollback procedure (update device configs)
- Maintain backups of old system configuration

---

### Big Bang Approach (High Risk)

Switch all devices to new system at once (e.g., during facility downtime).

**Only recommended if**:
- Facility has scheduled downtime (facility renovation, etc.)
- Small number of devices (<5)
- Extensive testing completed
- Staff trained on new system
- 24/7 support available

**Timeline**:
- Day 1: Deploy new system
- Day 1-2: Configure all devices
- Day 2-7: Intensive monitoring
- Week 2: Decommission old system

---

## Deployment Options

### Option 1: Docker Compose (Small Deployments)

**Best for**:
- Single hospital or clinic
- 1-10 imaging devices
- Simple networking
- Limited IT resources

**Setup**:
```bash
# Clone repository
git clone https://github.com/ohcnetwork/care-dicom-enabler-v2.git
cd care-dicom-enabler-v2

# Configure environment
cp .env.example .env
nano .env  # Edit configuration

# Start services
docker-compose up -d

# View logs
docker-compose logs -f

# Check status
docker-compose ps
```

**Maintenance**:
- Automatic restart on failure
- Log rotation with Docker logging driver
- Backup docker volumes regularly
- Update images with `docker-compose pull && docker-compose up -d`

---

### Option 2: Kubernetes (Large Scale)

**Best for**:
- Hospital networks
- 10+ imaging devices
- High availability requirements
- Centralized management
- Multi-site deployments

**Setup**:
```bash
# Install via Helm
helm repo add care https://charts.care.ohc.network
helm install care-dicom-enabler care/dicom-enabler \
  --set mwl.replicaCount=3 \
  --set storescp.replicaCount=2 \
  --set storage.type=s3 \
  --set storage.s3Bucket=dicom-prod

# Check status
kubectl get pods -n care
kubectl get svc -n care

# View logs
kubectl logs -n care deployment/care-mwl-service -f
```

**Features**:
- Automatic scaling (HPA)
- Rolling updates with zero downtime
- Health checks and auto-restart
- Load balancing for MWL/StoreSCP
- Persistent volumes for storage
- Secrets management

---

### Option 3: Cloud Managed Services

#### AWS Deployment

**Architecture**:
```
- ECS Fargate: Run containers without managing servers
- RDS PostgreSQL: Managed database with automatic backups
- S3: DICOM file storage with lifecycle policies
- ALB: Application Load Balancer for API
- CloudWatch: Logs and metrics
- Secrets Manager: Secure configuration storage
```

**Setup**:
```bash
# Deploy using CDK
cdk deploy CareRadiologyStack \
  --parameters DbPassword=secretpassword \
  --parameters S3Bucket=care-dicom-prod
```

**Cost Estimate** (per month):
- ECS Fargate (4 tasks, 0.5 vCPU, 1GB each): ~$50
- RDS PostgreSQL (db.t3.micro): ~$15
- S3 storage (1TB): ~$23
- Data transfer: ~$20
- ALB: ~$20
**Total: ~$128/month** (plus data transfer)

#### Azure Deployment

**Architecture**:
```
- Azure Container Instances: Serverless containers
- Azure Database for PostgreSQL: Managed database
- Azure Blob Storage: DICOM file storage
- Application Gateway: Load balancer
- Azure Monitor: Logs and metrics
```

#### GCP Deployment

**Architecture**:
```
- Cloud Run: Fully managed containers
- Cloud SQL: Managed PostgreSQL
- Cloud Storage: DICOM file storage
- Cloud Load Balancing: Traffic distribution
- Cloud Logging: Centralized logs
```

---

### Option 4: Hybrid (On-Premises + Cloud)

**Use Case**: Hospital network (on-premises) with cloud backup and disaster recovery.

**Architecture**:
```
On-Premises (Hospital):
- MWL SCP and Store SCP running locally
- Local PostgreSQL database
- Local NFS storage for DICOM files

Cloud (AWS/Azure/GCP):
- Store SCU uploads to cloud Django backend
- Cloud storage as backup
- Cloud-based OHIF viewer for remote access
- Disaster recovery standby
```

**Benefits**:
- Low latency for local devices
- Cloud backup and redundancy
- Remote access capabilities
- Compliance with data locality requirements

---

## Performance Considerations

### Benchmark Targets

| Metric | Target | Notes |
|--------|--------|-------|
| **Worklist Query Response** | < 500ms | C-FIND response time |
| **Image Storage Latency** | < 2s | C-STORE complete |
| **Upload Throughput** | 10+ images/sec | To Django/DCM4CHEE |
| **Concurrent Associations** | 50+ | Simultaneous DICOM connections |
| **Memory Usage (MWL)** | < 256MB | Per instance |
| **Memory Usage (StoreSCP)** | < 512MB | Per instance |
| **CPU Usage (idle)** | < 5% | No active transfers |
| **API Response Time** | < 200ms | 95th percentile |

### Optimization Strategies

1. **Database Indexing**
```sql
CREATE INDEX idx_instance_upload_status ON instance(upload_status);
CREATE INDEX idx_instance_created_at ON instance(created_at);
CREATE INDEX idx_study_patient_id ON study(patient_id);
CREATE INDEX idx_study_study_date ON study(study_date);
```

2. **Connection Pooling**
```csharp
services.AddDbContextPool<DicomDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.MaxBatchSize(100);
        npgsqlOptions.CommandTimeout(30);
    }),
    poolSize: 128  // Adjust based on load
);
```

3. **Async/Await Everywhere**
```csharp
// Bad: Blocking
var study = _db.Studies.Find(id);

// Good: Async
var study = await _db.Studies.FindAsync(id);
```

4. **Caching Strategy**
```csharp
// Cache server configurations
services.AddMemoryCache();
services.AddSingleton<IServerCache, ServerCache>();

// Cache implementation
public class ServerCache : IServerCache
{
    private readonly IMemoryCache _cache;

    public async Task<List<Server>> GetServersAsync()
    {
        return await _cache.GetOrCreateAsync("servers", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return await _db.Servers.ToListAsync();
        });
    }
}
```

5. **Batch Uploads**
```csharp
// Upload in batches of 10
var pendingInstances = await _db.Instances
    .Where(i => i.UploadStatus == "pending")
    .OrderBy(i => i.CreatedAt)
    .Take(10)
    .ToListAsync();

// Use Task.WhenAll for parallel uploads
var uploadTasks = pendingInstances.Select(i => UploadInstanceAsync(i));
await Task.WhenAll(uploadTasks);
```

---

## Security and Compliance

### Security Best Practices

1. **Network Segmentation**
   - Isolate DICOM services in dedicated VLAN
   - Firewall rules allowing only necessary ports
   - No direct internet access for DICOM services

2. **Authentication & Authorization**
   - JWT tokens with short expiration (15 minutes)
   - Refresh tokens for API access
   - Role-based access control (RBAC)
   - Audit logging for all admin actions

3. **Encryption**
   - TLS 1.3 for all HTTP API communication
   - Encrypted storage at rest (LUKS, S3 SSE)
   - Secrets stored in Kubernetes Secrets or Vault
   - Never log sensitive data (passwords, PHI)

4. **DICOM Security**
   - AE title whitelist for device authentication
   - Validate DICOM datasets for malicious content
   - Reject files with executable extensions
   - Limit max file size (e.g., 500MB per instance)

5. **Vulnerability Management**
   - Regular dependency updates (Dependabot)
   - Container image scanning (Trivy, Snyk)
   - Penetration testing annually
   - Security incident response plan

### HIPAA Compliance

1. **Access Controls**
   - Unique user accounts (no shared credentials)
   - Multi-factor authentication for admin access
   - Automatic session timeout (15 minutes)
   - Access logs retained for 7 years

2. **Audit Trail**
```csharp
public class AuditLog
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string UserId { get; set; }
    public string Action { get; set; }  // CREATE, READ, UPDATE, DELETE
    public string ResourceType { get; set; }  // Patient, Study, Server
    public string ResourceId { get; set; }
    public string IpAddress { get; set; }
    public string UserAgent { get; set; }
}

// Log all PHI access
await _auditLog.LogAsync(new AuditLog
{
    UserId = user.Id,
    Action = "READ",
    ResourceType = "Study",
    ResourceId = studyId,
    IpAddress = httpContext.Connection.RemoteIpAddress.ToString()
});
```

3. **Data Retention**
   - Automated backup (daily, 30-day retention)
   - Disaster recovery plan (RPO < 24hrs, RTO < 4hrs)
   - Secure data disposal (NIST 800-88 guidelines)

4. **Business Associate Agreements**
   - BAA with cloud providers (AWS, Azure, GCP)
   - BAA with Django CARE backend operators
   - BAA with any third-party vendors

---

## Cost-Benefit Analysis

### Current Windows System Costs (Annual)

| Item | Cost |
|------|------|
| Windows Server license (x2) | $1,600 |
| Visual Studio licenses (x2) | $900 |
| Windows Server management | $5,000 |
| MySQL licenses | $0 (open source) |
| Hardware (amortized) | $2,000 |
| **Total** | **$9,500/year** |

### Cross-Platform System Costs (Annual)

#### Option 1: On-Premises Docker

| Item | Cost |
|------|------|
| Linux server (Ubuntu) | $0 (free OS) |
| .NET 8 runtime | $0 (free) |
| PostgreSQL | $0 (open source) |
| Docker | $0 (free) |
| Hardware (amortized) | $1,500 |
| Maintenance | $2,000 |
| **Total** | **$3,500/year** |

**Savings: $6,000/year (63% reduction)**

#### Option 2: Cloud (AWS)

| Item | Cost |
|------|------|
| ECS Fargate | $600/year |
| RDS PostgreSQL | $180/year |
| S3 storage (1TB) | $276/year |
| Data transfer | $240/year |
| Load balancer | $240/year |
| **Total** | **$1,536/year** |

**Savings: $7,964/year (84% reduction)**

### Development Costs (One-Time)

| Phase | Developer Weeks | Cost (@$2,000/week) |
|-------|----------------|-------------------|
| Foundation | 4 | $8,000 |
| MWL Service | 3 | $6,000 |
| StoreSCP Service | 3 | $6,000 |
| StoreSCU Service | 3 | $6,000 |
| Management API | 3 | $6,000 |
| Web UI | 4 | $8,000 |
| Integration & Testing | 3 | $6,000 |
| Deployment & Migration | 3 | $6,000 |
| **Total** | **26 weeks** | **$52,000** |

**Break-Even**: 8.7 years at $6,000/year savings (on-premises)
**Break-Even**: 6.5 years at $7,964/year savings (cloud)

### Intangible Benefits (Not Quantified)

- ✅ Platform independence (deploy anywhere)
- ✅ Scalability (horizontal scaling)
- ✅ Developer productivity (modern tooling)
- ✅ Easier recruitment (popular tech stack)
- ✅ Integration capabilities (microservices, APIs)
- ✅ Cloud-native deployment options
- ✅ Reduced vendor lock-in
- ✅ Better security and compliance
- ✅ Faster feature development
- ✅ Community support (open source)

---

## Getting Started

### Quick Start (Evaluation)

Try the cross-platform version using Docker Compose:

```bash
# Clone repository
git clone https://github.com/ohcnetwork/care-dicom-enabler-v2.git
cd care-dicom-enabler-v2

# Start all services
docker-compose up -d

# Check status
docker-compose ps

# Test MWL query
findscu -v -S localhost 2008 -aet TEST -aec MODALITYSCP

# Test Store SCP
storescu -v localhost 2007 -aet TEST -aec STORAGESCP test.dcm

# Access Web UI
open http://localhost:3001
```

### Production Deployment

1. **Review Requirements**
   - Hardware/cloud resources
   - Network architecture
   - Compliance requirements (HIPAA, etc.)
   - Integration points with existing systems

2. **Choose Deployment Option**
   - Docker Compose (small scale)
   - Kubernetes (large scale)
   - Cloud managed services (AWS/Azure/GCP)

3. **Configure Environment**
   - Database connection strings
   - Storage settings (filesystem vs S3)
   - Django CARE backend URLs and API keys
   - SSL/TLS certificates

4. **Migration Plan**
   - Parallel operation or big bang
   - Device reconfiguration timeline
   - Staff training schedule
   - Rollback procedures

5. **Deploy and Validate**
   - Deploy to staging environment first
   - Test with DICOM emulators
   - Test with real devices (pilot group)
   - Monitor metrics and logs
   - Full rollout

---

## Conclusion

Migrating from the Windows .NET Framework-based DICOM Enabler to a cross-platform architecture provides **significant benefits**:

1. **Platform Independence**: Run on Linux, Windows, macOS, Docker, Kubernetes
2. **Cost Reduction**: Eliminate Windows Server licensing ($6,000-8,000/year savings)
3. **Scalability**: Horizontal scaling with load balancers and orchestration
4. **Modern Stack**: .NET 8 with active development and long-term support
5. **Cloud-Native**: First-class support for AWS, Azure, GCP
6. **Developer Experience**: Modern tooling, faster development cycles
7. **Integration**: Seamless REST API integration with Django CARE backend

**Recommended Approach**:
- **Technology**: .NET 8 for minimal migration effort and excellent performance
- **UI**: React web app instead of Windows Forms
- **Deployment**: Docker + Kubernetes for flexibility and scalability
- **Migration**: Parallel operation with phased device rollout

**Timeline**: 26 weeks (6 months) with 2-3 developers

**ROI**: Break-even in 6-9 years, but intangible benefits (scalability, modernization) justify investment sooner.

---

## Next Steps

1. **Stakeholder Approval**: Present this document to decision-makers
2. **Proof of Concept**: Build MWL service in .NET 8 to validate approach (2-3 weeks)
3. **Resource Allocation**: Assign development team and budget
4. **Kickoff**: Begin Phase 1 implementation
5. **Iterative Development**: Follow roadmap with regular reviews

For questions or to get started, contact the CARE development team or open an issue on GitHub.

---

**Document Version**: 1.0
**Last Updated**: 2026-05-15
**Maintained By**: CARE Development Team
