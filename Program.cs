using CommandLine;
using SMBLibrary;
using SMBLibrary.Adapters;
using SMBLibrary.Authentication.GSSAPI;
using SMBLibrary.Authentication.NTLM;
using SMBLibrary.Server;
using SMBLibrary.Win32;
using SMBLibrary.Win32.Security;
using SMBServer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace SuperSMBServer
{
    static class Program
    {
        static SMBLibrary.Server.SMBServer theServer;
        static SMBLibrary.Server.NameServer theNameServer;

        static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public class Options {
            [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
            public bool Verbose { get; set; }

            [Option('t', "transport", Default = SMBTransportType.DirectTCPTransport, Required = false,
                HelpText = "Choose Transport Type: 0 = NetBIOS Over TCP (Port 139), 1 = Direct TCP Transport (Port 445). Default: 1")]
            public SMBTransportType TransportType { get; set; }

            [Option('p', "protocol", Default = SMBProtocol.SMB1 | SMBProtocol.SMB2 | SMBProtocol.SMB3, Required = false,
                HelpText = "SMB Protocol (Flags): 1 = SMB 1.0/CIFS, 2 = SMB 2.0/2.1, 4 = SMB 3.0. Default: 7")]
            public SMBProtocol SMBProtocol { get; set; }
            
            [Option("listen", Default = "0.0.0.0", Required = false,
                HelpText = "IP address to listen to. Default is 0.0.0.0")]
            public string ListenIPAddress { get; set; }
        }
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args) {
            logger.Info("Super SMB Server started.");
            logger.Info("Options: {0}", args);

            CommandLine.Parser.Default.ParseArguments<Options>(args)
                .WithParsed(RunOptions)
                .WithNotParsed(HandleParseError);
        }

        static void RunOptions(Options opts) {
            List<ShareSettings> sharesSettings;
            try {
                sharesSettings = SettingsHelper.ReadSharesSettings();
            }
            catch (Exception) {
                logger.Error("Cannot read " + SettingsHelper.SettingsFileName);
                return;
            }

            List<AggregatedShareSettings> aggregatedSharesSettings;
            try {
                aggregatedSharesSettings = SettingsHelper.ReadAggregatedSharesSettings();
            }
            catch (Exception) {
                logger.Error("Cannot read " + SettingsHelper.SettingsFileName);
                return;
            }

            SMBShareCollection shares = new SMBShareCollection();
            foreach (ShareSettings shareSettings in sharesSettings) {
                FileSystemShare share = InitializeShare(shareSettings);
                shares.Add(share);
            }
            foreach (AggregatedShareSettings settings in aggregatedSharesSettings) {
                FileSystemShare share = InitializeAggFSShare(settings);
                shares.Add(share);
            }

            NTLMAuthenticationProviderBase authenticationMechanism = new IntegratedNTLMAuthenticationProvider();
            GSSProvider securityProvider = new GSSProvider(authenticationMechanism);

            theServer = new SMBLibrary.Server.SMBServer(shares, securityProvider);
            if (opts.Verbose) {
                theServer.LogEntryAdded += TheServer_LogEntryAdded;
            }
            bool enableSMB1 = (opts.SMBProtocol & SMBProtocol.SMB1) == SMBProtocol.SMB1;
            bool enableSMB2 = (opts.SMBProtocol & SMBProtocol.SMB2) == SMBProtocol.SMB2;
            bool enableSMB3 = (opts.SMBProtocol & SMBProtocol.SMB3) == SMBProtocol.SMB3;

            IPAddress listenAddr;
            if (!IPAddress.TryParse(opts.ListenIPAddress, out listenAddr)) {
                logger.Error(opts.ListenIPAddress + " is not a valid IP Address.");
                Environment.Exit(-1);
                return;
            }

            try {
                theServer.Start(listenAddr, opts.TransportType, enableSMB1, enableSMB2, enableSMB3);
                if (opts.TransportType == SMBTransportType.NetBiosOverTCP) {
                    if (listenAddr.AddressFamily == AddressFamily.InterNetwork && !IPAddress.Equals(listenAddr, IPAddress.Any)) {
                        IPAddress subnetMask = NetworkInterfaceHelper.GetSubnetMask(listenAddr);
                        theNameServer = new NameServer(listenAddr, subnetMask);
                        theNameServer.Start();
                    }
                }

                Console.Read();
            }
            catch (Exception ex) {
                logger.Error(ex.Message);
                Environment.Exit(-1);
                return;
            }

        }

        private static void TheServer_LogEntryAdded(object sender, Utilities.LogEntry e) {
            switch (e.Severity) {
                case Utilities.Severity.Critical:
                    logger.Fatal(e.Message);
                    break;
                case Utilities.Severity.Error:
                    logger.Error(e.Message);
                    break;
                case Utilities.Severity.Warning:
                    logger.Warn(e.Message);
                    break;
                case Utilities.Severity.Information:
                    logger.Info(e.Message);
                    break;
                case Utilities.Severity.Verbose:
                    logger.Info(e.Message);
                    break;
                case Utilities.Severity.Debug:
                    logger.Debug(e.Message);
                    break;
                case Utilities.Severity.Trace:
                default:
                    logger.Trace(e.Message);
                    break;
            }
        }

        static void HandleParseError(IEnumerable<Error> errs) {
            
        }

        public static FileSystemShare InitializeShare(ShareSettings shareSettings) {
            string shareName = shareSettings.ShareName;
            string sharePath = shareSettings.SharePath;
            List<string> readAccess = shareSettings.ReadAccess;
            List<string> writeAccess = shareSettings.WriteAccess;
            FileSystemShare share = new FileSystemShare(shareName, new NTDirectoryFileSystem(sharePath));
            share.AccessRequested += delegate (object sender, AccessRequestArgs args) {
                bool hasReadAccess = Contains(readAccess, "Users") || Contains(readAccess, args.UserName);
                bool hasWriteAccess = Contains(writeAccess, "Users") || Contains(writeAccess, args.UserName);
                if (args.RequestedAccess == FileAccess.Read) {
                    args.Allow = hasReadAccess;
                } else if (args.RequestedAccess == FileAccess.Write) {
                    args.Allow = hasWriteAccess;
                } else // FileAccess.ReadWrite
                  {
                    args.Allow = hasReadAccess && hasWriteAccess;
                }
            };
            return share;
        }

        public static FileSystemShare InitializeAggFSShare(AggregatedShareSettings settings) {
            string shareName = settings.ShareName;
            List<string> sharePaths = settings.SharePaths;
            List<string> readAccess = settings.ReadAccess;
            List<string> writeAccess = settings.WriteAccess;
            FileSystemShare share = new FileSystemShare(shareName, new NTFileSystemAdapter(new AggregatedFileSystem(sharePaths)));
            share.AccessRequested += delegate (object sender, AccessRequestArgs args) {
                bool hasReadAccess = Contains(readAccess, "Users") || Contains(readAccess, args.UserName);
                bool hasWriteAccess = Contains(writeAccess, "Users") || Contains(writeAccess, args.UserName);
                if (args.RequestedAccess == FileAccess.Read) {
                    args.Allow = hasReadAccess;
                } else if (args.RequestedAccess == FileAccess.Write) {
                    args.Allow = hasWriteAccess;
                } else // FileAccess.ReadWrite
                  {
                    args.Allow = hasReadAccess && hasWriteAccess;
                }
            };
            return share;
        }

        public static bool Contains(List<string> list, string value) {
            return (IndexOf(list, value) >= 0);
        }

        public static int IndexOf(List<string> list, string value) {
            for (int index = 0; index < list.Count; index++) {
                if (string.Equals(list[index], value, StringComparison.OrdinalIgnoreCase)) {
                    return index;
                }
            }
            return -1;
        }
    }
}
