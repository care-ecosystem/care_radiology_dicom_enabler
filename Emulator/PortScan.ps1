#Requires -Version 5.0
<#
DICOM Enabler - open port and AE title report.

Lists every TCP port this machine is listening on, checks each one for a DICOM service and
works out its AE title, then writes a report file. On every later run it compares against the
previous report and prints, on the terminal, the ports that are new since then (also ports that
have closed and ports whose AE title changed).

How the AE title is worked out
  A DICOM SCP does not announce its own AE title, so for each listening TCP port the script
  sends a DICOM A-ASSOCIATE-RQ (Verification SOP class) and reads the answer:
    1. Port configured in <install folder>\cfg\common.cfg (mwlport / sscpport): the configured
       AE title is tried first. Accepted = AE title verified.
    2. Otherwise the called AE title ANY-SCP is tried. If the SCP accepts it, it does not check
       the called AE title, and the report says ANY (not enforced).
    3. If the SCP rejects with "called AE title not recognised", each candidate title (the AE
       titles in common.cfg plus -CandidateAETitles) is tried until one is accepted.
       None accepted = UNKNOWN.
  The association is released straight away; no data is sent or stored. DICOM services log
  these connections (calling AE title PORTSCAN by default).
  Ports in -SkipProbePorts (Windows system ports and MySQL by default) are listed but not probed.

Report
  <ReportDir>\PortReport_<yyyy-MM-dd_HH-mm-ss>.csv  (opens in Excel)
  Columns: Protocol, Port, LocalAddress, ProcessId, ProcessName, Services, IsDicom, AETitle,
           AESource, Detail, ChangeStatus
  The newest earlier PortReport_*.csv in the same folder is the one compared against.

Parameters (all optional)
  -ReportDir          Folder for reports  (default: C:\ProgramData\CARE DICOM Enabler\PortReports)
  -ConfigPath         common.cfg to read   (default: <install folder>\cfg\common.cfg, where the
                      install folder is the parent of this script's folder)
  -CandidateAETitles  Extra AE titles to try on SCPs that enforce their called AE title
  -CallingAETitle     Calling AE title used by the probe (default: PORTSCAN)
  -SkipProbePorts     Ports listed but never probed
  -TimeoutMs          Connect / read timeout per probe in ms (default: 2000)
  -NoProbe            List ports only; no DICOM probing
  -IncludeUdp         Also list UDP ports (UDP ports change often, so expect more "new" ports)

Live listening
  After the port scan and the report, the script keeps running and shows, live on the terminal,
  what it sees on the network until you press q:
    - every new TCP connection (source -> destination:port)
    - every DICOM association request: the calling AE title (the sender), the called AE title (the
      SCP it is set to talk to), the IPs, the SCP's port, what it asked for (Worklist / Storage /
      MPPS / Verification ...) and whether the SCP accepted it
  Use it to find what a modality is configured to use: run a worklist query, a test send or the
  "Verify / Echo" button on the modality while the script is listening. When you press q the
  DICOM associations seen are written to Capture_<yyyy-MM-dd_HH-mm-ss>.csv in the report folder.
  It needs Administrator rights (raw sockets) and nothing else: no tools to install, works offline.
  Only IPv4 traffic to or from THIS machine is seen, so run it on the modality PC or on the DICOM
  Enabler PC. DICOM over TLS cannot be read (the connection is still listed).
  If Windows Firewall or an antivirus blocks raw sockets the script says so and skips listening.

  -NoListen           Scan and report only; do not listen afterwards
  -DicomOnly          While listening, show only DICOM association lines (hide plain connections)

  -SearchConfig       Runs INSTEAD of the scan and the listening. Searches config files
                      (.ini .cfg .conf .config .xml .json .properties .yaml .yml .cnf) under
                      Program Files, ProgramData, user profiles and the top-level folders of every
                      fixed drive, plus the registry, for AE title, port and host settings. Values
                      next to words like password / token are masked.
  -SearchPath         Extra folders for -SearchConfig (e.g. D:\ModalitySoftware)
#>
[CmdletBinding()]
param(
    [string]$ReportDir = (Join-Path $env:ProgramData 'CARE DICOM Enabler\PortReports'),
    [string]$ConfigPath,
    [string[]]$CandidateAETitles = @('STORESCP', 'STORAGESCP', 'STORE_SCP', 'MODALITYSCP', 'MWLSCP',
                                     'MWL_SCP', 'WORKLIST', 'MWL', 'PACS', 'ORTHANC', 'DCM4CHEE',
                                     'CONQUESTSRV1', 'DICOM', 'SCP', 'ECHOSCP'),
    [ValidateLength(1, 16)]
    [string]$CallingAETitle = 'PORTSCAN',
    [int[]]$SkipProbePorts = @(135, 139, 445, 3306, 3389, 5985, 5986, 33060, 47001),
    [ValidateRange(200, 60000)]
    [int]$TimeoutMs = 2000,
    [switch]$NoProbe,
    [switch]$IncludeUdp,
    [switch]$NoListen,
    [switch]$DicomOnly,
    [switch]$SearchConfig,
    [string[]]$SearchPath = @()
)

$ErrorActionPreference = 'Stop'

$AnyAETitle     = 'ANY-SCP'
$VerificationUid = '1.2.840.10008.1.1'
$ImplicitLEUid  = '1.2.840.10008.1.2'
$AppContextUid  = '1.2.840.10008.3.1.1.1'
$ImplClassUid   = '2.25.170914112845287616471380922719283104823'
$ImplVersion    = 'CARE_PORTSCAN'

$RunStamp   = Get-Date -Format 'yyyy-MM-dd_HH-mm-ss'
$ReportName = "PortReport_$RunStamp.csv"

function Write-Step { param([string]$Message) Write-Host ""; Write-Host "== $Message ==" -ForegroundColor Cyan }
function Write-Ok   { param([string]$Message) Write-Host "  [OK] $Message" -ForegroundColor Green }
function Write-Info { param([string]$Message) Write-Host "  [INFO] $Message" }
function Write-Warn { param([string]$Message) Write-Host "  [WARN] $Message" -ForegroundColor Yellow }
function Write-Fail { param([string]$Message) Write-Host "  [FAIL] $Message" -ForegroundColor Red }

# ---------------------------------------------------------------------------------------------
# DICOM upper layer helpers (PS3.8)
# ---------------------------------------------------------------------------------------------

function Add-U16 { param($List, [int]$Value) $List.Add([byte](($Value -shr 8) -band 0xFF)); $List.Add([byte]($Value -band 0xFF)) }
function Add-U32 {
    param($List, [long]$Value)
    foreach ($shift in 24, 16, 8, 0) { $List.Add([byte](($Value -shr $shift) -band 0xFF)) }
}

function New-PduItem {
    param([byte]$Type, [byte[]]$Body)
    $item = New-Object System.Collections.Generic.List[byte]
    $item.Add($Type); $item.Add(0)
    Add-U16 $item $Body.Length
    $item.AddRange($Body)
    return ,$item.ToArray()
}

function Get-AsciiBytes { param([string]$Text) return ,[Text.Encoding]::ASCII.GetBytes($Text) }

function New-AssociateRq {
    param([string]$CalledAE, [string]$CallingAE)

    $pcBody = New-Object System.Collections.Generic.List[byte]
    $pcBody.AddRange([byte[]](1, 0, 0, 0))   # presentation context id 1
    $pcBody.AddRange((New-PduItem 0x30 (Get-AsciiBytes $VerificationUid)))
    $pcBody.AddRange((New-PduItem 0x40 (Get-AsciiBytes $ImplicitLEUid)))

    $maxLen = New-Object System.Collections.Generic.List[byte]
    Add-U32 $maxLen 16384
    $uiBody = New-Object System.Collections.Generic.List[byte]
    $uiBody.AddRange((New-PduItem 0x51 $maxLen.ToArray()))
    $uiBody.AddRange((New-PduItem 0x52 (Get-AsciiBytes $ImplClassUid)))
    $uiBody.AddRange((New-PduItem 0x55 (Get-AsciiBytes $ImplVersion)))

    $body = New-Object System.Collections.Generic.List[byte]
    Add-U16 $body 1                 # protocol version
    Add-U16 $body 0                 # reserved
    $body.AddRange((Get-AsciiBytes $CalledAE.PadRight(16).Substring(0, 16)))
    $body.AddRange((Get-AsciiBytes $CallingAE.PadRight(16).Substring(0, 16)))
    $body.AddRange((New-Object byte[] 32))
    $body.AddRange((New-PduItem 0x10 (Get-AsciiBytes $AppContextUid)))
    $body.AddRange((New-PduItem 0x20 $pcBody.ToArray()))
    $body.AddRange((New-PduItem 0x50 $uiBody.ToArray()))

    $pdu = New-Object System.Collections.Generic.List[byte]
    $pdu.Add(0x01); $pdu.Add(0)
    Add-U32 $pdu $body.Count
    $pdu.AddRange($body)
    return ,$pdu.ToArray()
}

function Read-Exact {
    param($Stream, [int]$Count)
    $buffer = New-Object byte[] $Count
    $offset = 0
    while ($offset -lt $Count) {
        $n = $Stream.Read($buffer, $offset, $Count - $offset)
        if ($n -le 0) { return $null }
        $offset += $n
    }
    return ,$buffer
}

function Get-RejectText {
    param([int]$Source, [int]$Reason)
    switch ($Source) {
        1 {
            switch ($Reason) {
                2 { return 'application context not supported' }
                3 { return 'calling AE title not recognised' }
                7 { return 'called AE title not recognised' }
                default { return 'rejected by the SCP (no reason given)' }
            }
        }
        2 { if ($Reason -eq 2) { return 'protocol version not supported' } else { return 'rejected by the SCP (no reason given)' } }
        3 { if ($Reason -eq 1) { return 'SCP temporarily congested' } else { return 'SCP local limit exceeded' } }
    }
    return "rejected (source $Source, reason $Reason)"
}

# Sends an A-ASSOCIATE-RQ and classifies the answer.
# Status: Accepted | Rejected | Aborted | NotDicom | NoConnect
function Invoke-DicomProbe {
    param([string]$Address, [int]$Port, [string]$CalledAE)

    $result = [pscustomobject]@{ Status = 'NoConnect'; Source = 0; Reason = 0; Text = '' }
    $client = $null
    try {
        $ip = [Net.IPAddress]::Parse($Address)
        $client = New-Object System.Net.Sockets.TcpClient($ip.AddressFamily)
        $connect = $client.BeginConnect($ip, $Port, $null, $null)
        if (-not $connect.AsyncWaitHandle.WaitOne($TimeoutMs)) {
            $result.Text = "no connection within $TimeoutMs ms"
            return $result
        }
        $client.EndConnect($connect)

        $stream = $client.GetStream()
        $stream.ReadTimeout = $TimeoutMs
        $stream.WriteTimeout = $TimeoutMs
        $rq = New-AssociateRq -CalledAE $CalledAE -CallingAE $CallingAETitle
        $stream.Write($rq, 0, $rq.Length)

        $result.Status = 'NotDicom'
        $header = Read-Exact $stream 6
        if ($null -eq $header) { $result.Text = 'closed the connection without a DICOM answer'; return $result }
        $length = ([long]$header[2] -shl 24) -bor ([long]$header[3] -shl 16) -bor ([long]$header[4] -shl 8) -bor [long]$header[5]
        $type = $header[0]
        if ($header[1] -ne 0 -or @(2, 3, 7) -notcontains $type -or $length -gt 65536 -or $length -lt 4) {
            $result.Text = 'answered, but not with a DICOM association PDU'
            return $result
        }
        $body = Read-Exact $stream ([int]$length)
        if ($null -eq $body) { $result.Text = 'DICOM answer was cut short'; return $result }

        switch ($type) {
            2 {
                $result.Status = 'Accepted'
                $result.Text = 'association accepted'
                # Presentation context result (item 0x21): byte 2 of the item body, 0 = accepted
                $i = 68
                while ($i + 4 -le $body.Length) {
                    $itemLen = ([int]$body[$i + 2] -shl 8) -bor $body[$i + 3]
                    if ($body[$i] -eq 0x21 -and $i + 6 -lt $body.Length -and $body[$i + 6] -ne 0) {
                        $result.Text = 'association accepted (Verification / C-ECHO not supported)'
                    }
                    $i += 4 + $itemLen
                }
                try {
                    $release = [byte[]](5, 0, 0, 0, 0, 4, 0, 0, 0, 0)
                    $stream.Write($release, 0, $release.Length)
                    [void](Read-Exact $stream 6)
                } catch { }
            }
            3 {
                $result.Status = 'Rejected'
                $result.Source = $body[2]
                $result.Reason = $body[3]
                $result.Text = Get-RejectText -Source $body[2] -Reason $body[3]
            }
            7 {
                $result.Status = 'Aborted'
                $result.Text = 'association aborted by the SCP'
            }
        }
        return $result
    } catch {
        $err = $_.Exception
        if ($err.InnerException) { $err = $err.InnerException }
        if ($result.Status -eq 'NoConnect') { $result.Text = "could not connect: $($err.Message)" }
        else { $result.Text = 'no DICOM answer (timed out or connection reset)' }
        return $result
    } finally {
        if ($client) { $client.Close() }
    }
}

# Works out IsDicom / AETitle / AESource / Detail for one TCP port.
function Resolve-AETitle {
    param([string]$Address, [int]$Port, [string]$ConfiguredAE, [string]$ConfiguredFor, [string[]]$Candidates)

    $info = [ordered]@{ IsDicom = 'No'; AETitle = ''; AESource = ''; Detail = '' }

    $firstAE = $AnyAETitle
    if ($ConfiguredAE) { $firstAE = $ConfiguredAE }
    $probe = Invoke-DicomProbe -Address $Address -Port $Port -CalledAE $firstAE

    if (@('NotDicom', 'NoConnect') -contains $probe.Status) {
        $info.Detail = $probe.Text
        if ($ConfiguredAE) {
            $info.Detail = "common.cfg expects $ConfiguredFor '$ConfiguredAE' here, but the port $($probe.Text)"
        }
        return $info
    }

    $info.IsDicom = 'Yes'
    if ($probe.Status -eq 'Accepted') {
        if ($ConfiguredAE) {
            $info.AETitle = $ConfiguredAE
            $info.AESource = 'common.cfg (verified)'
        } else {
            $info.AETitle = 'ANY (not enforced)'
            $info.AESource = 'Probe'
        }
        $info.Detail = $probe.Text
        return $info
    }
    if ($probe.Status -eq 'Aborted') {
        $info.AETitle = 'UNKNOWN'
        $info.AESource = 'Probe'
        $info.Detail = $probe.Text
        if ($ConfiguredAE) { $info.AETitle = $ConfiguredAE; $info.AESource = 'common.cfg (not verified)' }
        return $info
    }

    # Rejected
    if (-not ($probe.Source -eq 1 -and $probe.Reason -eq 7)) {
        $info.AETitle = 'UNKNOWN'
        $info.AESource = 'Probe'
        $info.Detail = $probe.Text
        if ($probe.Source -eq 1 -and $probe.Reason -eq 3) {
            $info.Detail = "$($probe.Text) - rerun with -CallingAETitle set to a title this SCP allows"
        }
        if ($ConfiguredAE) { $info.AETitle = $ConfiguredAE; $info.AESource = 'common.cfg (not verified)' }
        return $info
    }

    # The SCP checks its called AE title: try the candidates
    $tried = @($firstAE)
    foreach ($candidate in $Candidates) {
        if ($tried -contains $candidate) { continue }
        $tried += $candidate
        $p = Invoke-DicomProbe -Address $Address -Port $Port -CalledAE $candidate
        if ($p.Status -eq 'Accepted') {
            $info.AETitle = $candidate
            $info.AESource = 'Probe (candidate matched)'
            $info.Detail = $p.Text
            if ($ConfiguredAE) { $info.Detail = "$($p.Text); common.cfg has '$ConfiguredAE', which the SCP rejected" }
            return $info
        }
    }
    $info.AETitle = 'UNKNOWN'
    $info.AESource = 'Probe'
    $info.Detail = "SCP checks its called AE title and none of the $($tried.Count) titles tried matched - add it with -CandidateAETitles"
    if ($ConfiguredAE) { $info.Detail = "common.cfg has '$ConfiguredAE' but the SCP rejected it; no candidate title matched either" }
    return $info
}

# ---------------------------------------------------------------------------------------------
# Port listing
# ---------------------------------------------------------------------------------------------

function Get-ListeningTcp {
    if (Get-Command Get-NetTCPConnection -ErrorAction SilentlyContinue) {
        return @(Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue |
            ForEach-Object { [pscustomobject]@{ Address = $_.LocalAddress; Port = [int]$_.LocalPort; ProcessId = [int]$_.OwningProcess } })
    }
    # Older Windows without the NetTCPIP module
    $rows = @()
    foreach ($line in (netstat -ano -p TCP) + (netstat -ano -p TCPv6)) {
        if ($line -match '^\s*TCP\s+(\S+):(\d+)\s+\S+\s+LISTENING\s+(\d+)') {
            $rows += [pscustomobject]@{ Address = $Matches[1].Trim('[', ']'); Port = [int]$Matches[2]; ProcessId = [int]$Matches[3] }
        }
    }
    return $rows
}

function Get-OpenUdp {
    if (-not (Get-Command Get-NetUDPEndpoint -ErrorAction SilentlyContinue)) { return @() }
    return @(Get-NetUDPEndpoint -ErrorAction SilentlyContinue |
        ForEach-Object { [pscustomobject]@{ Address = $_.LocalAddress; Port = [int]$_.LocalPort; ProcessId = [int]$_.OwningProcess } })
}

# ---------------------------------------------------------------------------------------------
# Live listening and -SearchConfig
# ---------------------------------------------------------------------------------------------

# Compiled once per session with the C# compiler that ships with .NET Framework (offline).
# Kept to C# 5 because Windows PowerShell 5.1 compiles with that.
$script:ScanSource = @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

public class CareAssoc
{
    public string ClientIp;
    public string ServerIp;
    public int ServerPort;
    public string CalledAE;
    public string CallingAE;
    public string Services;
    public string Result;
    public int Seen;
}

// Finds DICOM A-ASSOCIATE-RQ / -AC / -RJ PDUs in a pcap or pcapng file.
public class CarePcapScan
{
    private readonly List<CareAssoc> list = new List<CareAssoc>();
    private readonly Dictionary<string, CareAssoc> byKey = new Dictionary<string, CareAssoc>();
    private readonly Dictionary<string, CareAssoc> byFlow = new Dictionary<string, CareAssoc>();
    private static readonly byte[] AppCtx = Encoding.ASCII.GetBytes("1.2.840.10008.3.1.1.1");

    public bool DicomOnly;
    private readonly Queue<string> events = new Queue<string>();
    private readonly Dictionary<string, DateTime> lastEmit = new Dictionary<string, DateTime>();

    public List<CareAssoc> Associations { get { return list; } }

    // Feeds one raw IPv4 packet (starting at the IP header), as read from a raw socket.
    public void FeedIp(byte[] d, int len)
    {
        Frame(d, 0, len, 101);
    }

    public string[] Drain()
    {
        string[] r = events.ToArray();
        events.Clear();
        return r;
    }

    // The same packet can be seen twice (for example on loopback), so identical lines within a
    // second are shown once.
    private void Emit(string line)
    {
        DateTime now = DateTime.Now, last;
        if (lastEmit.TryGetValue(line, out last) && (now - last).TotalMilliseconds < 1000) { return; }
        lastEmit[line] = now;
        if (lastEmit.Count > 2000) { lastEmit.Clear(); }
        events.Enqueue(now.ToString("HH:mm:ss") + "  " + line);
    }

    private static uint U32(byte[] b, int o, bool le)
    {
        if (le) { return (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24)); }
        return (uint)((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]);
    }

    private static int U16(byte[] b, int o, bool le)
    {
        return le ? (b[o] | (b[o + 1] << 8)) : ((b[o] << 8) | b[o + 1]);
    }

    private static int BE16(byte[] b, int o) { return (b[o] << 8) | b[o + 1]; }

    private static string Ip(byte[] d, int o, int n)
    {
        byte[] a = new byte[n];
        Array.Copy(d, o, a, 0, n);
        return new IPAddress(a).ToString();
    }

    private void Frame(byte[] d, int off, int len, int link)
    {
        int p = off, end = off + len, ethType, ipLen, srcOff, dstOff, proto;
        if (link == 1)
        {
            if (len < 14) { return; }
            ethType = BE16(d, p + 12);
            p += 14;
            while ((ethType == 0x8100 || ethType == 0x88A8) && p + 4 <= end) { ethType = BE16(d, p + 2); p += 4; }
        }
        else if (link == 101)
        {
            int v = d[p] >> 4;
            ethType = v == 4 ? 0x0800 : (v == 6 ? 0x86DD : 0);
        }
        else { return; }

        if (ethType == 0x0800)
        {
            if (p + 20 > end) { return; }
            int ihl = (d[p] & 0x0F) * 4;
            if (ihl < 20 || p + ihl > end) { return; }
            if ((BE16(d, p + 6) & 0x1FFF) != 0) { return; }
            proto = d[p + 9];
            int total = BE16(d, p + 2);
            if (total >= ihl && p + total < end) { end = p + total; }
            ipLen = 4; srcOff = p + 12; dstOff = p + 16;
            p += ihl;
        }
        else if (ethType == 0x86DD)
        {
            if (p + 40 > end) { return; }
            proto = d[p + 6];
            int plen = BE16(d, p + 4);
            if (p + 40 + plen < end) { end = p + 40 + plen; }
            ipLen = 16; srcOff = p + 8; dstOff = p + 24;
            p += 40;
        }
        else { return; }

        if (proto != 6 || p + 20 > end) { return; }
        int sport = BE16(d, p), dport = BE16(d, p + 2);
        int doff = (d[p + 12] >> 4) * 4;
        if (doff < 20 || p + doff > end) { return; }
        p += doff;
        int n = end - p;
        if (!DicomOnly && (d[p + 13 - doff] & 0x12) == 0x02)
        {
            Emit("CONNECT  " + Ip(d, srcOff, ipLen) + ":" + sport + " -> " + Ip(d, dstOff, ipLen) + ":" + dport);
        }
        if (n < 10 || d[p + 1] != 0) { return; }
        byte t = d[p];
        if (t == 1 || t == 2)
        {
            if (!IsAssocPdu(d, p, n)) { return; }
            if (t == 1) { AddRq(d, p, n, Ip(d, srcOff, ipLen), sport, Ip(d, dstOff, ipLen), dport); }
            else { AddReply(d, p, Ip(d, srcOff, ipLen), sport, Ip(d, dstOff, ipLen), dport); }
        }
        else if (t == 3 && n == 10 && BE32(d, p + 2) == 4)
        {
            AddReply(d, p, Ip(d, srcOff, ipLen), sport, Ip(d, dstOff, ipLen), dport);
        }
    }

    private static uint BE32(byte[] b, int o) { return U32(b, o, false); }

    // A-ASSOCIATE-RQ and -AC share one layout: version 1, called/calling AE, application context.
    private static bool IsAssocPdu(byte[] d, int p, int n)
    {
        if (n < 99 || d[p + 6] != 0 || d[p + 7] != 1) { return false; }
        if (d[p + 74] != 0x10 || d[p + 75] != 0 || d[p + 76] != 0 || d[p + 77] != 21) { return false; }
        for (int i = 0; i < AppCtx.Length; i++) { if (d[p + 78 + i] != AppCtx[i]) { return false; } }
        for (int i = 10; i < 42; i++) { if (d[p + i] != 0 && (d[p + i] < 0x20 || d[p + i] > 0x7E)) { return false; } }
        return true;
    }

    private static string Ae(byte[] d, int o)
    {
        return Encoding.ASCII.GetString(d, o, 16).Trim(' ', '\0');
    }

    private void AddRq(byte[] d, int p, int n, string cip, int sport, string sip, int dport)
    {
        string called = Ae(d, p + 10);
        string calling = Ae(d, p + 26);
        string services = Classify(d, p, n);
        string key = cip + "|" + sip + "|" + dport + "|" + called + "|" + calling;
        CareAssoc a;
        if (!byKey.TryGetValue(key, out a))
        {
            a = new CareAssoc();
            a.ClientIp = cip; a.ServerIp = sip; a.ServerPort = dport;
            a.CalledAE = called; a.CallingAE = calling;
            a.Services = services; a.Result = "(no reply seen)";
            byKey[key] = a;
            list.Add(a);
        }
        else if (services.Length > a.Services.Length)
        {
            a.Services = services;
        }
        a.Seen++;
        Emit("DICOM    " + cip + " -> " + sip + ":" + dport + "   Calling AE: " + calling + "   Called AE: " + called + "   Asks for: " + services);
        byFlow[cip + ":" + sport + ">" + sip + ":" + dport] = a;
    }

    private void AddReply(byte[] d, int p, string sip, int sport, string cip, int cport)
    {
        // The reply travels server -> client, so the flow key is the reverse of the request's.
        CareAssoc a;
        if (!byFlow.TryGetValue(cip + ":" + cport + ">" + sip + ":" + sport, out a)) { return; }
        if (d[p] == 2) { a.Result = "Accepted"; EmitResult(a); return; }
        int source = d[p + 8], reason = d[p + 9];
        if (source == 1 && reason == 7) { a.Result = "Rejected - called AE title not recognised"; }
        else if (source == 1 && reason == 3) { a.Result = "Rejected - calling AE title not recognised"; }
        else if (source == 1 && reason == 2) { a.Result = "Rejected - application context not supported"; }
        else { a.Result = "Rejected (source " + source + ", reason " + reason + ")"; }
        EmitResult(a);
    }

    private void EmitResult(CareAssoc a)
    {
        Emit("REPLY    " + a.ServerIp + ":" + a.ServerPort + " -> " + a.ClientIp + "   Called AE: " + a.CalledAE + "   " + a.Result);
    }

    // What the sender asked for, from the abstract syntaxes in its presentation contexts.
    // Only the first TCP segment is read, which is enough for the first contexts.
    private static string Classify(byte[] d, int p, int n)
    {
        List<string> kinds = new List<string>();
        int end = p + n;
        long declared = (long)p + 6 + BE32(d, p + 2);
        if (declared < end) { end = (int)declared; }
        int pos = p + 74;
        while (pos + 4 <= end)
        {
            int it = d[pos], il = BE16(d, pos + 2);
            if (it == 0x20)
            {
                int s = pos + 8, se = Math.Min(pos + 4 + il, end);
                while (s + 4 <= se)
                {
                    int sl = BE16(d, s + 2);
                    if (d[s] == 0x30 && s + 4 + sl <= se)
                    {
                        string k = Kind(Encoding.ASCII.GetString(d, s + 4, sl).Trim(' ', '\0'));
                        if (!kinds.Contains(k)) { kinds.Add(k); }
                    }
                    s += 4 + sl;
                }
            }
            pos += 4 + il;
        }
        return string.Join("; ", kinds.ToArray());
    }

    private static string Kind(string uid)
    {
        if (uid == "1.2.840.10008.1.1") { return "Verification (C-ECHO)"; }
        if (uid == "1.2.840.10008.5.1.4.31") { return "Worklist (MWL C-FIND)"; }
        if (uid == "1.2.840.10008.3.1.2.3.3" || uid == "1.2.840.10008.3.1.2.3.5") { return "MPPS"; }
        if (uid.StartsWith("1.2.840.10008.1.20")) { return "Storage Commitment"; }
        if (uid.StartsWith("1.2.840.10008.5.1.4.1.1.")) { return "Storage (C-STORE)"; }
        if (uid.StartsWith("1.2.840.10008.5.1.4.1.2.")) { return "Query/Retrieve"; }
        return "Other (" + uid + ")";
    }
}

// Live capture of IPv4 packets with raw sockets (one per local address), fed into CarePcapScan.
public class CareLiveSniffer
{
    private readonly CarePcapScan parser = new CarePcapScan();
    private readonly List<System.Net.Sockets.Socket> sockets = new List<System.Net.Sockets.Socket>();
    private readonly List<System.Threading.Thread> threads = new List<System.Threading.Thread>();
    private volatile bool stop;

    public CareLiveSniffer(bool dicomOnly) { parser.DicomOnly = dicomOnly; }

    // Returns the local addresses being listened on; failures are added to errors.
    public List<string> Start(List<string> errors)
    {
        List<string> bound = new List<string>();
        List<IPAddress> addrs = new List<IPAddress>();
        foreach (System.Net.NetworkInformation.NetworkInterface nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) { continue; }
            foreach (System.Net.NetworkInformation.UnicastIPAddressInformation ua in nic.GetIPProperties().UnicastAddresses)
            {
                if (ua.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !addrs.Contains(ua.Address)) { addrs.Add(ua.Address); }
            }
        }
        foreach (IPAddress a in addrs)
        {
            try
            {
                System.Net.Sockets.Socket s = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Raw, System.Net.Sockets.ProtocolType.IP);
                s.Bind(new IPEndPoint(a, 0));
                s.SetSocketOption(System.Net.Sockets.SocketOptionLevel.IP, System.Net.Sockets.SocketOptionName.HeaderIncluded, true);
                try { s.ReceiveBufferSize = 4 * 1024 * 1024; } catch (Exception) { }
                s.IOControl(System.Net.Sockets.IOControlCode.ReceiveAll, new byte[] { 1, 0, 0, 0 }, new byte[4]);
                sockets.Add(s);
                bound.Add(a.ToString());
                System.Threading.Thread t = new System.Threading.Thread(delegate () { Loop(s); });
                t.IsBackground = true;
                threads.Add(t);
                t.Start();
            }
            catch (Exception ex) { errors.Add(a.ToString() + ": " + ex.Message); }
        }
        return bound;
    }

    private void Loop(System.Net.Sockets.Socket s)
    {
        byte[] buf = new byte[65536];
        int failures = 0;
        while (!stop)
        {
            int n;
            try { n = s.Receive(buf); }
            catch (Exception) { if (stop || ++failures > 50) { break; } System.Threading.Thread.Sleep(20); continue; }
            if (n < 20) { continue; }
            try { lock (parser) { parser.FeedIp(buf, n); } } catch (Exception) { }
        }
    }

    public string[] Drain() { lock (parser) { return parser.Drain(); } }

    public List<CareAssoc> Associations() { lock (parser) { return new List<CareAssoc>(parser.Associations); } }

    public void Stop()
    {
        stop = true;
        foreach (System.Net.Sockets.Socket s in sockets) { try { s.Close(); } catch (Exception) { } }
        foreach (System.Threading.Thread t in threads) { try { t.Join(500); } catch (Exception) { } }
    }
}

public class CareHit
{
    public string Source;
    public string Kind;
    public string Location;
    public string Line;
    public string Text;
}

// Searches config files and the registry for AE title / port / host settings.
public static class CareConfigScan
{
    private static readonly Regex AeRx = new Regex(@"(ae[\s_.\-]?title|\baet\b|called[\s_.\-]?ae|calling[\s_.\-]?ae|local[\s_.\-]?ae|remote[\s_.\-]?ae|station[\s_.\-]?ae|scp[\s_.\-]?ae)", RegexOptions.IgnoreCase);
    private static readonly Regex SvcRx = new Regex(@"(worklist|\bmwl\b|\bpacs\b|dicom|dcm|store[\s_.\-]?scp|storage|\bscp\b|\bscu\b|modality|archive)", RegexOptions.IgnoreCase);
    private static readonly Regex AddrRx = new Regex(@"(port|host|\bip\b|address|server|url|endpoint)", RegexOptions.IgnoreCase);
    private static readonly Regex SecretRx = new Regex(@"((?:pass(?:word)?|pwd|secret|token|api[_\-]?key)\w*[\s=:""'>]+)([^\s""'<;,]+)", RegexOptions.IgnoreCase);

    private static string Mask(string s) { return SecretRx.Replace(s, "$1***"); }

    public static List<CareHit> ScanFiles(string[] roots, string[] excludeNames, string[] exts, int maxTotal, out int filesScanned)
    {
        List<CareHit> hits = new List<CareHit>();
        Stack<string> stack = new Stack<string>();
        int scanned = 0;
        foreach (string r in roots) { if (Directory.Exists(r)) { stack.Push(r); } }
        while (stack.Count > 0 && hits.Count < maxTotal)
        {
            string dir = stack.Pop();
            string[] files;
            try { files = Directory.GetFiles(dir); } catch (Exception) { files = new string[0]; }
            foreach (string f in files)
            {
                string ext = Path.GetExtension(f).TrimStart('.').ToLowerInvariant();
                if (Array.IndexOf(exts, ext) < 0) { continue; }
                scanned++;
                ScanFile(f, hits, maxTotal);
                if (hits.Count >= maxTotal) { break; }
            }
            string[] subs;
            try { subs = Directory.GetDirectories(dir); } catch (Exception) { subs = new string[0]; }
            foreach (string s in subs)
            {
                string name = Path.GetFileName(s);
                if (Array.FindIndex(excludeNames, delegate (string x) { return string.Equals(x, name, StringComparison.OrdinalIgnoreCase); }) >= 0) { continue; }
                try { if ((File.GetAttributes(s) & FileAttributes.ReparsePoint) != 0) { continue; } } catch (Exception) { continue; }
                stack.Push(s);
            }
        }
        filesScanned = scanned;
        return hits;
    }

    private static void ScanFile(string path, List<CareHit> hits, int maxTotal)
    {
        byte[] b;
        try
        {
            FileInfo fi = new FileInfo(path);
            if (fi.Length == 0 || fi.Length > 2 * 1024 * 1024) { return; }
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                b = new byte[fs.Length];
                int got = 0;
                while (got < b.Length)
                {
                    int r = fs.Read(b, got, b.Length - got);
                    if (r <= 0) { break; }
                    got += r;
                }
                if (got < b.Length) { Array.Resize(ref b, got); }
            }
        }
        catch (Exception) { return; }

        string text;
        if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE) { text = Encoding.Unicode.GetString(b, 2, b.Length - 2); }
        else if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF) { text = Encoding.BigEndianUnicode.GetString(b, 2, b.Length - 2); }
        else
        {
            int lim = Math.Min(b.Length, 1024);
            for (int i = 0; i < lim; i++) { if (b[i] == 0) { return; } }
            int s0 = (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) ? 3 : 0;
            text = Encoding.UTF8.GetString(b, s0, b.Length - s0);
        }

        string[] lines = text.Split('\n');
        int perFile = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length < 3 || line.Length > 300) { continue; }
            string kind = null;
            if (AeRx.IsMatch(line)) { kind = "AE title"; }
            else if (SvcRx.IsMatch(line) && AddrRx.IsMatch(line)) { kind = "Service address"; }
            if (kind == null) { continue; }
            CareHit h = new CareHit();
            h.Source = "File"; h.Kind = kind; h.Location = path;
            h.Line = (i + 1).ToString(); h.Text = Mask(line);
            hits.Add(h);
            perFile++;
            if (perFile >= 15 || hits.Count >= maxTotal) { break; }
        }
    }

    public static List<CareHit> ScanRegistry(int maxTotal)
    {
        List<CareHit> hits = new List<CareHit>();
        string[] skip = new string[] { "Microsoft", "Classes", "Policies", "Wow6432Node", "Google", "Mozilla", "Intel", "NVIDIA Corporation", "Realtek", "VMware, Inc." };
        WalkRoot(hits, Registry.LocalMachine, @"SOFTWARE", @"HKLM\SOFTWARE", skip, maxTotal);
        WalkRoot(hits, Registry.LocalMachine, @"SOFTWARE\WOW6432Node", @"HKLM\SOFTWARE\WOW6432Node", skip, maxTotal);
        WalkRoot(hits, Registry.CurrentUser, @"SOFTWARE", @"HKCU\SOFTWARE", skip, maxTotal);
        return hits;
    }

    private static void WalkRoot(List<CareHit> hits, RegistryKey hive, string path, string display, string[] skip, int maxTotal)
    {
        try
        {
            using (RegistryKey k = hive.OpenSubKey(path))
            {
                if (k != null) { Walk(hits, k, display, 0, skip, maxTotal); }
            }
        }
        catch (Exception) { }
    }

    private static void Walk(List<CareHit> hits, RegistryKey key, string display, int depth, string[] skip, int maxTotal)
    {
        if (hits.Count >= maxTotal || depth > 8) { return; }
        try
        {
            foreach (string name in key.GetValueNames())
            {
                string kind = null;
                if (AeRx.IsMatch(name) || (name.Length == 0 && AeRx.IsMatch(display))) { kind = "AE title"; }
                else if ((SvcRx.IsMatch(display) || SvcRx.IsMatch(name)) && AddrRx.IsMatch(name)) { kind = "Service address"; }
                if (kind == null) { continue; }
                object v = key.GetValue(name);
                string text;
                if (v == null) { text = ""; }
                else if (v is string[]) { text = string.Join(", ", (string[])v); }
                else if (v is byte[]) { text = "(binary)"; }
                else { text = v.ToString(); }
                if (text.Length > 200) { text = text.Substring(0, 200); }
                CareHit h = new CareHit();
                h.Source = "Registry"; h.Kind = kind; h.Location = display;
                h.Line = name.Length == 0 ? "(Default)" : name;
                h.Text = Mask(h.Line + " = " + text);
                hits.Add(h);
                if (hits.Count >= maxTotal) { return; }
            }
            foreach (string sub in key.GetSubKeyNames())
            {
                if (depth == 0 && Array.FindIndex(skip, delegate (string x) { return string.Equals(x, sub, StringComparison.OrdinalIgnoreCase); }) >= 0) { continue; }
                try
                {
                    using (RegistryKey child = key.OpenSubKey(sub))
                    {
                        if (child != null) { Walk(hits, child, display + "\\" + sub, depth + 1, skip, maxTotal); }
                    }
                }
                catch (Exception) { }
            }
        }
        catch (Exception) { }
    }
}
'@

function Initialize-ScanTypes {
    if ('CarePcapScan' -as [type]) { return }
    Add-Type -TypeDefinition $script:ScanSource -Language CSharp
}

function Save-ModeCsv {
    param($Rows, [string]$Prefix)
    try {
        if (-not (Test-Path -LiteralPath $ReportDir)) { New-Item -ItemType Directory -Path $ReportDir -Force | Out-Null }
        $path = Join-Path $ReportDir "${Prefix}_$RunStamp.csv"
        $Rows | Export-Csv -LiteralPath $path -NoTypeInformation -Encoding UTF8
        Write-Ok "Report written: $path"
    } catch {
        Write-Fail "Could not write the report to $ReportDir ($($_.Exception.Message)). Pass -ReportDir to use another folder."
        $script:ModeFailed = $true
    }
}

# Listens on the network and prints connections / DICOM associations live until q is pressed.
function Invoke-ListenMode {
    Write-Step 'Listening for modality connections'
    if (-not $isAdmin) {
        Write-Warn 'Listening needs Administrator rights - skipped. Right-click PortScan.bat and choose "Run as administrator".'
        return
    }
    try { Initialize-ScanTypes } catch {
        Write-Warn "Could not compile the listener: $($_.Exception.Message) - listening skipped."
        return
    }
    $sniffer = New-Object CareLiveSniffer($DicomOnly.IsPresent)
    $errors = New-Object 'System.Collections.Generic.List[string]'
    try {
        $bound = @($sniffer.Start($errors))
        foreach ($e in $errors) { Write-Warn "Could not listen on $e" }
        if ($bound.Count -eq 0) {
            Write-Warn 'Could not listen on any network address (raw sockets may be blocked by a firewall or antivirus) - listening skipped.'
            return
        }
        Write-Ok "Listening on: $($bound -join ', ')"
        Write-Host ''
        Write-Host '  Now run a worklist query, a test send or "Verify / Echo" on the modality.' -ForegroundColor Yellow
        Write-Host '  Lines appear below as they happen.  Press q to stop.' -ForegroundColor Yellow
        Write-Host ''
        $canKey = $true
        $quit = $false
        while (-not $quit) {
            foreach ($line in @($sniffer.Drain())) {
                $color = 'Gray'
                if ($line -match '^\S+\s+DICOM ') { $color = 'Yellow' }
                elseif ($line -match '^\S+\s+REPLY ') { if ($line -match 'Accepted$') { $color = 'Green' } else { $color = 'Red' } }
                Write-Host $line -ForegroundColor $color
            }
            if ($canKey) {
                try {
                    while ([Console]::KeyAvailable) {
                        $k = [Console]::ReadKey($true)
                        if ($k.KeyChar -eq 'q' -or $k.KeyChar -eq 'Q') { $quit = $true }
                    }
                } catch {
                    $canKey = $false
                    Write-Warn 'Keyboard input is not available here - press Ctrl+C to stop.'
                }
            }
            Start-Sleep -Milliseconds 200
        }
    } finally {
        $sniffer.Stop()
    }
    foreach ($line in @($sniffer.Drain())) { Write-Host $line }
    Write-Info 'Stopped listening.'

    $assocs = @($sniffer.Associations())
    Write-Step 'DICOM associations seen'
    if ($assocs.Count -eq 0) {
        Write-Info 'None. Check that the modality connected while this ran, and that this is the modality PC or the PC it connects to.'
        return
    }
    Write-Host '  Calling AE = the device that connected.  Called AE = the AE title it asked for on the other side' -ForegroundColor DarkGray
    Write-Host '  (what its worklist / storage settings are set to).' -ForegroundColor DarkGray
    $assocs | Sort-Object ServerPort, ClientIp | ForEach-Object {
        [pscustomobject]@{ 'Calling AE (sender)' = $_.CallingAE; From = $_.ClientIp; 'Called AE (SCP)' = $_.CalledAE; To = "$($_.ServerIp):$($_.ServerPort)"; Services = $_.Services; Result = $_.Result; Seen = $_.Seen }
    } | Format-Table -AutoSize -Wrap | Out-String -Width 220 | Write-Host
    $csvRows = @($assocs | ForEach-Object {
        [pscustomobject]@{ ClientAddress = $_.ClientIp; CallingAE = $_.CallingAE; ServerAddress = $_.ServerIp; ServerPort = $_.ServerPort; CalledAE = $_.CalledAE; Services = $_.Services; Result = $_.Result; Seen = $_.Seen }
    })
    Save-ModeCsv -Rows $csvRows -Prefix 'Capture'
}

function Invoke-ConfigSearchMode {
    Write-Step 'Searching config files and the registry for AE title and port settings'
    try { Initialize-ScanTypes } catch {
        Write-Fail "Could not compile the search code: $($_.Exception.Message)"
        $script:ModeFailed = $true
        return
    }

    $roots = New-Object System.Collections.Generic.List[string]
    foreach ($r in @($env:ProgramFiles, ${env:ProgramFiles(x86)}, $env:ProgramData)) { if ($r) { $roots.Add($r) } }
    $usersDir = Join-Path $env:SystemDrive 'Users'
    if (Test-Path -LiteralPath $usersDir) {
        foreach ($u in (Get-ChildItem -LiteralPath $usersDir -Directory -ErrorAction SilentlyContinue)) {
            foreach ($sub in 'AppData\Roaming', 'AppData\Local', 'Documents', 'Desktop') { $roots.Add((Join-Path $u.FullName $sub)) }
        }
    }
    $skipTop = @('Windows', 'Program Files', 'Program Files (x86)', 'ProgramData', 'Users', '$Recycle.Bin', 'System Volume Information',
                 'Recovery', 'PerfLogs', 'Config.Msi', 'Documents and Settings', 'MSOCache')
    foreach ($drive in [IO.DriveInfo]::GetDrives()) {
        if ($drive.DriveType -ne 'Fixed' -or -not $drive.IsReady) { continue }
        foreach ($d in (Get-ChildItem -LiteralPath $drive.RootDirectory.FullName -Directory -Force -ErrorAction SilentlyContinue)) {
            if ($skipTop -notcontains $d.Name) { $roots.Add($d.FullName) }
        }
    }
    foreach ($p in $SearchPath) { if ($p) { $roots.Add($p) } }
    $rootList = @($roots | Where-Object { Test-Path -LiteralPath $_ -PathType Container } | Select-Object -Unique)
    foreach ($p in $SearchPath) { if ($p -and -not (Test-Path -LiteralPath $p)) { Write-Warn "-SearchPath folder not found: $p" } }

    $exclude = @('Windows', 'WinSxS', '$Recycle.Bin', 'System Volume Information', 'node_modules', '.git', 'Microsoft', 'Packages', 'Temp',
                 'Cache', 'Code Cache', 'GPUCache', 'CrashDumps', 'Backups', 'PortReports', 'Microsoft Visual Studio', 'dotnet', 'Windows Kits',
                 'WindowsApps', 'Common Files', 'Windows Defender', 'Windows NT', 'Internet Explorer', 'WindowsPowerShell', 'Google',
                 'Mozilla', 'Mozilla Firefox', 'Java', 'MySQL')
    $exts = @('ini', 'cfg', 'conf', 'config', 'xml', 'json', 'properties', 'yaml', 'yml', 'cnf')

    Write-Info "Searching $($rootList.Count) folder(s) - this can take a few minutes..."
    $fileCount = 0
    $hits = @()
    try {
        $hits += @([CareConfigScan]::ScanFiles([string[]]$rootList, [string[]]$exclude, [string[]]$exts, 500, [ref]$fileCount))
    } catch { Write-Warn "File search stopped early: $($_.Exception.Message)" }
    Write-Ok "$fileCount config file(s) read, $($hits.Count) matching line(s)"

    Write-Info 'Searching the registry...'
    try {
        $regHits = @([CareConfigScan]::ScanRegistry(300))
        $hits += $regHits
        Write-Ok "$($regHits.Count) matching registry value(s)"
    } catch { Write-Warn "Registry search stopped early: $($_.Exception.Message)" }

    if ($hits.Count -eq 0) {
        Write-Warn 'Nothing found. Pass -SearchPath with the modality software folder, or check the vendor documentation.'
        return
    }

    $rows = @($hits | Sort-Object @{ e = { if ($_.Kind -eq 'AE title') { 0 } else { 1 } } }, Source, Location, @{ e = { [int]($_.Line -replace '\D', '0') } } |
        ForEach-Object { [pscustomobject]@{ Source = $_.Source; Kind = $_.Kind; Location = $_.Location; Line = $_.Line; Text = $_.Text } })

    Write-Step 'Settings found (AE title lines first)'
    $shown = 0
    foreach ($row in $rows) {
        if ($shown -ge 60) { Write-Info "... $($rows.Count - 60) more in the report file"; break }
        $where = if ($row.Source -eq 'File') { "$($row.Location):$($row.Line)" } else { $row.Location }
        $color = if ($row.Kind -eq 'AE title') { 'Yellow' } else { 'Gray' }
        Write-Host "  [$($row.Kind)] $where" -ForegroundColor $color
        Write-Host "      $($row.Text)"
        $shown++
    }
    Write-Info 'Values next to password / token / secret are masked. Settings can be stale or belong to other software - confirm against the modality screens.'
    Save-ModeCsv -Rows $rows -Prefix 'ConfigSearch'
}

# The address to probe a listener on: loopback when it listens on all interfaces or loopback.
function Get-ProbeAddress {
    param([string[]]$Addresses)
    foreach ($a in $Addresses) { if (@('0.0.0.0', '127.0.0.1') -contains $a) { return '127.0.0.1' } }
    foreach ($a in $Addresses) { if (@('::', '::1') -contains $a) { return '::1' } }
    return $Addresses[0]
}

# ---------------------------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------------------------

Write-Host "Run started: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') on $env:COMPUTERNAME"

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) { Write-Warn 'Not running as Administrator - some process and service names may show as blank.' }

# -SearchConfig runs instead of the port scan
if ($SearchConfig) {
    $script:ModeFailed = $false
    Invoke-ConfigSearchMode
    if ($script:ModeFailed) { exit 1 }
    exit 0
}

# --- configured AE titles ---
Write-Step 'Reading DICOM Enabler configuration'
if (-not $ConfigPath) { $ConfigPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'cfg\common.cfg' }
$ConfiguredPorts = @{}   # port -> @{ AE; For }
$ConfiguredAEs = @()
if (Test-Path -LiteralPath $ConfigPath -PathType Leaf) {
    try {
        [xml]$cfg = Get-Content -LiteralPath $ConfigPath -Raw
        $c = $cfg.configurations
        foreach ($pair in @(@('mwlaetitle', 'mwlport', 'MWL SCP'), @('sscpaetitle', 'sscpport', 'Store SCP'))) {
            $ae = [string]$c.($pair[0]); $port = [string]$c.($pair[1])
            if ($ae -and $port -match '^\d+$') {
                $ConfiguredPorts[[int]$port] = @{ AE = $ae.Trim(); For = $pair[2] }
                Write-Info "$($pair[2]): AE title '$($ae.Trim())' on port $port"
            }
        }
        foreach ($key in 'mwlaetitle', 'sscpaetitle', 'sscuaetitle', 'callingaetitle') {
            $v = [string]$c.$key
            if ($v) { $ConfiguredAEs += $v.Trim() }
        }
        Write-Ok "Read $ConfigPath"
    } catch {
        Write-Warn "Could not read $ConfigPath ($($_.Exception.Message)) - continuing with probing only."
    }
} else {
    Write-Warn "$ConfigPath not found - continuing with probing only (pass -ConfigPath to point at it)."
}
$Candidates = @($ConfiguredAEs + $CandidateAETitles | Where-Object { $_ } | Select-Object -Unique)

# --- open ports ---
Write-Step 'Listing open ports'
$endpoints = @()
foreach ($e in (Get-ListeningTcp)) { $endpoints += [pscustomobject]@{ Protocol = 'TCP'; Address = $e.Address; Port = $e.Port; ProcessId = $e.ProcessId } }
if ($IncludeUdp) {
    foreach ($e in (Get-OpenUdp)) { $endpoints += [pscustomobject]@{ Protocol = 'UDP'; Address = $e.Address; Port = $e.Port; ProcessId = $e.ProcessId } }
}
if ($endpoints.Count -eq 0) {
    Write-Fail 'No open ports could be listed. Nothing was written.'
    exit 1
}

$processNames = @{}
foreach ($p in (Get-Process -ErrorAction SilentlyContinue)) { $processNames[[int]$p.Id] = $p.ProcessName }
$serviceNames = @{}
try {
    foreach ($s in (Get-CimInstance Win32_Service -Filter 'ProcessId > 0' -ErrorAction Stop)) {
        $procId = [int]$s.ProcessId
        if ($serviceNames.ContainsKey($procId)) { $serviceNames[$procId] += "; $($s.Name)" } else { $serviceNames[$procId] = $s.Name }
    }
} catch { Write-Warn "Could not read Windows services: $($_.Exception.Message)" }

# One row per protocol + port (IPv4 and IPv6 listeners on the same port are merged)
$ports = @($endpoints | Group-Object Protocol, Port | ForEach-Object {
    $first = $_.Group[0]
    $pids = @($_.Group | Select-Object -ExpandProperty ProcessId -Unique)
    [pscustomobject]@{
        Protocol    = $first.Protocol
        Port        = $first.Port
        Addresses   = @($_.Group | Select-Object -ExpandProperty Address -Unique)
        ProcessId   = ($pids -join '; ')
        ProcessName = (@($pids | ForEach-Object { $processNames[$_] } | Where-Object { $_ } | Select-Object -Unique) -join '; ')
        Services    = (@($pids | ForEach-Object { $serviceNames[$_] } | Where-Object { $_ } | Select-Object -Unique) -join '; ')
    }
} | Sort-Object Protocol, Port)
Write-Ok "$($ports.Count) open port(s) found"

# --- DICOM probing ---
if ($NoProbe) {
    Write-Step 'DICOM probing skipped (-NoProbe)'
} else {
    Write-Step 'Checking each TCP port for a DICOM service'
}
$rows = @()
$tcpCount = @($ports | Where-Object { $_.Protocol -eq 'TCP' }).Count
$done = 0
foreach ($p in $ports) {
    $info = [ordered]@{ IsDicom = ''; AETitle = ''; AESource = ''; Detail = '' }
    $configured = $null
    if ($p.Protocol -eq 'TCP') { $configured = $ConfiguredPorts[$p.Port] }

    if ($p.Protocol -eq 'UDP') {
        $info.IsDicom = 'N/A'
        $info.Detail = 'UDP - DICOM uses TCP only'
    } elseif ($NoProbe) {
        $info.IsDicom = 'Not checked'
        if ($configured) { $info.AETitle = $configured.AE; $info.AESource = 'common.cfg (not verified)' }
    } elseif ($SkipProbePorts -contains $p.Port) {
        $info.IsDicom = 'Not checked'
        $info.Detail = 'port is in -SkipProbePorts'
        $done++
    } else {
        $done++
        Write-Progress -Activity 'Checking ports for DICOM' -Status "Port $($p.Port) ($done of $tcpCount)" -PercentComplete ([math]::Min(100, 100 * $done / [math]::Max(1, $tcpCount)))
        $cfgAE = $null; $cfgFor = $null
        if ($configured) { $cfgAE = $configured.AE; $cfgFor = $configured.For }
        $info = Resolve-AETitle -Address (Get-ProbeAddress $p.Addresses) -Port $p.Port -ConfiguredAE $cfgAE -ConfiguredFor $cfgFor -Candidates $Candidates
        if ($info.IsDicom -eq 'Yes') { Write-Info ("Port {0,-6} DICOM  AE title: {1}  ({2})" -f $p.Port, $info.AETitle, $info.AESource) }
    }

    $rows += [pscustomobject]@{
        Protocol     = $p.Protocol
        Port         = $p.Port
        LocalAddress = ($p.Addresses -join '; ')
        ProcessId    = $p.ProcessId
        ProcessName  = $p.ProcessName
        Services     = $p.Services
        IsDicom      = $info.IsDicom
        AETitle      = $info.AETitle
        AESource     = $info.AESource
        Detail       = $info.Detail
        ChangeStatus = ''
    }
}
Write-Progress -Activity 'Checking ports for DICOM' -Completed

foreach ($port in $ConfiguredPorts.Keys) {
    if (-not ($rows | Where-Object { $_.Protocol -eq 'TCP' -and $_.Port -eq $port })) {
        Write-Warn "$($ConfiguredPorts[$port].For) port $port (AE title '$($ConfiguredPorts[$port].AE)') from common.cfg is not listening."
    }
}

# --- compare with the previous report ---
Write-Step 'Comparing with the previous report'
$previous = $null
if (Test-Path -LiteralPath $ReportDir -PathType Container) {
    $previous = Get-ChildItem -LiteralPath $ReportDir -Filter 'PortReport_*.csv' -File |
        Where-Object { $_.Name -ne $ReportName } | Sort-Object Name -Descending | Select-Object -First 1
}

$newRows = @(); $closedRows = @(); $aeChanged = @()
if ($previous) {
    try {
        $prevRows = @(Import-Csv -LiteralPath $previous.FullName)
        $prevByKey = @{}
        foreach ($r in $prevRows) { $prevByKey["$($r.Protocol)/$($r.Port)"] = $r }
        $currentKeys = @{}
        foreach ($r in $rows) {
            $key = "$($r.Protocol)/$($r.Port)"
            $currentKeys[$key] = $true
            if (-not $prevByKey.ContainsKey($key)) {
                $r.ChangeStatus = 'New'
                $newRows += $r
            } elseif ($r.IsDicom -eq 'Yes' -and $prevByKey[$key].AETitle -and $r.AETitle -ne $prevByKey[$key].AETitle) {
                $r.ChangeStatus = "AE title changed (was $($prevByKey[$key].AETitle))"
                $aeChanged += [pscustomobject]@{ Row = $r; Was = $prevByKey[$key].AETitle }
            } else {
                $r.ChangeStatus = 'Unchanged'
            }
        }
        foreach ($r in $prevRows) { if (-not $currentKeys.ContainsKey("$($r.Protocol)/$($r.Port)")) { $closedRows += $r } }
        Write-Info "Previous report: $($previous.FullName)"
    } catch {
        Write-Warn "Could not read the previous report $($previous.FullName) ($($_.Exception.Message)) - no comparison made."
        $previous = $null
    }
}
if (-not $previous) {
    foreach ($r in $rows) { $r.ChangeStatus = 'First run' }
    Write-Info "No previous report to compare with in $ReportDir - this run is the baseline for the next one."
}

# --- write the report ---
Write-Step 'Writing the report'
try {
    if (-not (Test-Path -LiteralPath $ReportDir)) { New-Item -ItemType Directory -Path $ReportDir -Force | Out-Null }
    $reportPath = Join-Path $ReportDir $ReportName
    $rows | Export-Csv -LiteralPath $reportPath -NoTypeInformation -Encoding UTF8
    Write-Ok "Report written: $reportPath"
} catch {
    Write-Fail "Could not write the report to $ReportDir ($($_.Exception.Message)). Pass -ReportDir to use another folder."
    exit 1
}

# --- terminal summary ---
Write-Step 'Open ports'
$rows | Format-Table Protocol, Port, ProcessName, IsDicom, AETitle, ChangeStatus -AutoSize | Out-String -Width 200 | Write-Host

if ($previous) {
    Write-Step "Changes since $($previous.Name)"
    if ($newRows.Count -gt 0) {
        Write-Host "  NEW open port(s): $($newRows.Count)" -ForegroundColor Yellow
        foreach ($r in $newRows) {
            $ae = ''
            if ($r.IsDicom -eq 'Yes') { $ae = "  DICOM AE title: $($r.AETitle)" }
            Write-Host ("    + {0} {1,-6} {2}{3}" -f $r.Protocol, $r.Port, $r.ProcessName, $ae) -ForegroundColor Yellow
        }
    } else {
        Write-Ok 'No new open ports.'
    }
    foreach ($r in $closedRows) { Write-Host ("    - {0} {1,-6} {2}  (closed since the previous report)" -f $r.Protocol, $r.Port, $r.ProcessName) }
    foreach ($c in $aeChanged) { Write-Host ("    * TCP {0,-6} AE title changed: {1} -> {2}" -f $c.Row.Port, $c.Was, $c.Row.AETitle) -ForegroundColor Yellow }
}

if (-not $NoListen) {
    $script:ModeFailed = $false
    Invoke-ListenMode
    if ($script:ModeFailed) { exit 1 }
}
exit 0
