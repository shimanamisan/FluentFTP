﻿using System.Text.RegularExpressions;
using System.Threading;
using System.Threading;
using System.Threading.Tasks;

namespace FluentFTP {
	public partial class FtpClient {

		/// <summary>
		/// Opens a FXP PASV connection between the source FTP Server and the destination FTP Server
		/// </summary>
		/// <param name="remoteClient">FtpClient instance of the destination FTP Server</param>
		/// <returns>A data stream ready to be used</returns>
		protected FtpFxpSession OpenPassiveFXPConnection(FtpClient remoteClient, bool trackProgress) {
			FtpReply reply, reply2;
			Match m;
			FtpClient sourceClient = this;
			FtpClient destinationClient = remoteClient;
			FtpClient progressClient = null;

			// create a new connection to the target FTP server to track progress
			// if progress tracking is enabled during this FXP transfer
			if (trackProgress) {
				progressClient = (FtpClient)remoteClient.Clone();
				progressClient.Status.AutoDispose = true;
				progressClient.Status.CopyFrom(remoteClient.Status);
				progressClient.Connect();
				progressClient.SetWorkingDirectory(remoteClient.GetWorkingDirectory());
			}

			sourceClient.SetDataType(sourceClient.FXPDataType);
			destinationClient.SetDataType(destinationClient.FXPDataType);

			// send PASV/CPSV commands to destination FTP server to get passive port to be used from source FTP server
			// first try with PASV - commonly supported by all servers
			if (!(reply = destinationClient.Execute("PASV")).Success) {

				// then try with CPSV - known to be supported by glFTPd server
				// FIXES #666 - glFTPd server - 435 Failed TLS negotiation on data channel
				if (!(reply2 = destinationClient.Execute("CPSV")).Success) {
					throw new FtpCommandException(reply);
				}
				else {

					// use the CPSV response and extract the port from it
					reply = reply2;
				}
			}

			// extract port from response
			m = Regex.Match(reply.Message, @"(?<quad1>\d+)," + @"(?<quad2>\d+)," + @"(?<quad3>\d+)," + @"(?<quad4>\d+)," + @"(?<port1>\d+)," + @"(?<port2>\d+)");
			if (!m.Success || m.Groups.Count != 7) {
				throw new FtpException("Malformed PASV response: " + reply.Message);
			}

			// Instruct source server to open a connection to the destination Server

			if (!(reply = sourceClient.Execute($"PORT {m.Value}")).Success) {
				throw new FtpCommandException(reply);
			}

			// the FXP session stores the active connections used for this FXP transfer
			return new FtpFxpSession {
				SourceServer = sourceClient,
				TargetServer = destinationClient,
				ProgressServer = progressClient,
			};
		}

#if ASYNC

		/// <summary>
		/// Opens a FXP PASV connection between the source FTP Server and the destination FTP Server
		/// </summary>
		/// <param name="remoteClient">Valid FTP connection to the destination FTP Server</param>
		/// <returns>A data stream ready to be used</returns>
		protected async Task<FtpFxpSession> OpenPassiveFXPConnectionAsync(FtpClient remoteClient, bool trackProgress, CancellationToken token) {
			FtpReply reply, reply2;
			Match m;
			FtpClient sourceClient = this;
			FtpClient destinationClient = remoteClient;
			FtpClient progressClient = null;

			// create a new connection to the target FTP server to track progress
			// if progress tracking is enabled during this FXP transfer
			if (trackProgress) {
				progressClient = (FtpClient)remoteClient.Clone();
				progressClient.Status.AutoDispose = true;
				progressClient.Status.CopyFrom(remoteClient.Status);
				await progressClient.ConnectAsync(token);
				await progressClient.SetWorkingDirectoryAsync(await remoteClient.GetWorkingDirectoryAsync(token), token);
			}

			await sourceClient.SetDataTypeAsync(sourceClient.FXPDataType, token);
			await destinationClient.SetDataTypeAsync(destinationClient.FXPDataType, token);

			// send PASV/CPSV commands to destination FTP server to get passive port to be used from source FTP server
			// first try with PASV - commonly supported by all servers
			if (!(reply = await destinationClient.ExecuteAsync("PASV", token)).Success) {

				// then try with CPSV - known to be supported by glFTPd server
				// FIXES #666 - glFTPd server - 435 Failed TLS negotiation on data channel
				if (!(reply2 = await destinationClient.ExecuteAsync("CPSV", token)).Success) {
					throw new FtpCommandException(reply);
				}
				else {

					// use the CPSV response and extract the port from it
					reply = reply2;
				}
			}

			// extract port from response
			m = Regex.Match(reply.Message, @"(?<quad1>\d+)," + @"(?<quad2>\d+)," + @"(?<quad3>\d+)," + @"(?<quad4>\d+)," + @"(?<port1>\d+)," + @"(?<port2>\d+)");

			if (!m.Success || m.Groups.Count != 7) {
				throw new FtpException("Malformed PASV response: " + reply.Message);
			}

			// Instruct source server to open a connection to the destination Server

			if (!(reply = await sourceClient.ExecuteAsync($"PORT {m.Value}", token)).Success) {
				throw new FtpCommandException(reply);
			}

			// the FXP session stores the active connections used for this FXP transfer
			return new FtpFxpSession {
				SourceServer = sourceClient,
				TargetServer = destinationClient,
				ProgressServer = progressClient,
			};
		}

#endif

	}
}
