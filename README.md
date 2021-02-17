# Super SMB Server

Super SMB Server is an SMB file server that supports folder aggregation. It can serve files located in several folders on local disks as a single SMB share.

## What is folder aggregation?

<img src="https://user-images.githubusercontent.com/1035538/108232907-355c6e00-717e-11eb-8f02-62e712b9280e.png" width="320" />


## Build

Prerequisites: Visual Studio 2019.

- Open the solution with Visual Studio 2019
- Build

## Before running

###### Referenced from "SMBLibrary".

By default, Windows already use ports 139 and 445. There're several ways to free / utilize those ports:

##### Method 1: Disable Windows File and Printer Sharing server completely:
###### Windows XP/2003:
1. For every network adapter: Uncheck 'File and Printer Sharing for Microsoft Networks".
2. Navigate to 'HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\NetBT\Parameters' and set 'SMBDeviceEnabled' to '0' (this will free port 445).
3. Reboot.

###### Windows 7/8/2008/2012:
Disable the "Server" service (p.s. "TCP\IP NETBIOS Helper" should be enabled).

##### Method 2: Use Windows File Sharing AND SMBLibrary:
Windows bind port 139 to the first IP addres of every adapter, while port 445 is bound globally.
This means that if you'll disable port 445 (or block it using a firewall), you'll be able to use a different service on port 139 for every IP address.

###### Additional Notes:
* To free port 139 for a given adapter, go to 'Internet Protocol (TCP/IP) Properties' > Advanced > WINS, and select 'Disable NetBIOS over TCP/IP'.
Uncheck 'File and Printer Sharing for Microsoft Networks' to ensure Windows will not answer to SMB traffic on port 445 for this adapter.

* It's important to note that disabling NetBIOS over TCP/IP will also disable NetBIOS name service for that adapter (a.k.a. WINS), This service uses UDP port 137.
SMBLibrary offers a name service of its own.

* You can install a virtual network adapter driver for Windows to be used solely with SMBLibrary:
  - You can install the 'Microsoft Loopback adapter' and use it for server-only communication with SMBLibrary.

###### Windows 7/8/2008/2012:
* It's possible to prevent Windows from using port 445 by removing all of the '\Device\Tcpip_{..}' and '\Device\Tcpip6_{..}' entries from the `Bind' registry key under 'HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\LanmanServer\Linkage'.  

* if you want localhost access from Windows explorer to work as expected, you must specify the IP address that you selected (\\\\127.0.0.1 or \\\\localhost will not work as expected), in addition, I have observed that when connecting to the first IP address of a given adapter, Windows will only attempt to connect to port 445.

##### Method 3: Use an IP address that is invisible to Windows File Sharing:
Using PCap.Net you can programmatically setup a virtual Network adapter and intercept SMB traffic (similar to how a virtual machine operates), You should use the ARP protocol to notify the network about the new IP address, and then process the incoming SMB traffic using SMBLibrary, good luck! 

## Usage

### 1. Setup
All share configurations are stored in Settings.xml. Setup shares by editing it.

```xml
<?xml version="1.0" encoding="utf-8" ?>
<Settings>
  <Shares>
    <Share Name="Shared" Path="C:\Shared">
      <ReadAccess Accounts="*"></ReadAccess>
      <WriteAccess Accounts="Administrator,Test"></WriteAccess>
    </Share>
  </Shares>
  <AggregatedShares>
    <AggregatedShare Name="AggShare">
      <Path>C:\Shared1</Path>
      <Path>C:\Shared2</Path>
      <Path>C:\Shared3</Path>
      <ReadAccess Accounts="*"></ReadAccess>
      <WriteAccess Accounts="Administrator,Test"></WriteAccess>
    </AggregatedShare>
  </AggregatedShares>
</Settings>
```

Here "Accounts" in "ReadAccess" and "WriteAccess" nodes refers to Windows account names.

### 2. Run

```
$ SuperSMBServer
```
More options:
```
$ SuperSMBServer --help

  -t, --transport    (Default: DirectTCPTransport) Transport Type: 0 = NetBIOS Over TCP (Port 139), 1 = Direct TCP Transport (Port 445). Default: 1

  -p, --protocol     (Default: SMB1, SMB2, SMB3) SMB Protocol (Flags): 1 = SMB 1.0/CIFS, 2 = SMB 2.0/2.1, 4 = SMB 3.0. Default: 7

  --listen           (Default: 0.0.0.0) IP address to listen on. Default is 0.0.0.0

  --help             Display this help screen.
```
## Thanks

This project is based on [SMBLibrary](https://github.com/TalAloni/SMBLibrary). 