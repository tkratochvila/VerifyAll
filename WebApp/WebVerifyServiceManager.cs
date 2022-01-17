using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Web.WebSockets;
using System.Text.Json;
using InterLayerLib;
using System.Data;
using System.Text;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace webApp
{
    // TO-DO: when deleting WebVerifyServiceInfo from directory, make sure noone accessing the WebVerifyServiceInfo and the socket is closed (need for lock in WebVerifyServiceInfo)
    // TO-DO: some periodicall cleanUp of sessions and their directories dependent on activity and/or timeout
    public sealed class WebVerifyServiceManager
    {
        private static int bufferSize = 1024;

        private static Dictionary<string, WebVerifyServiceInfo> _services = new Dictionary<string, WebVerifyServiceInfo>();
        private static readonly object _serviceLock = new object();

        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static WebVerifyServiceManager()
        {
        }

        public static void init()
        {
            sessionsCleanUp();
        }

        private static void sessionsCleanUp()
        {
            lock (_serviceLock)
            {
                // go thru directories and clean up those that are no longer used
                var dirs = Directory.GetDirectories(Directory.GetCurrentDirectory());

                foreach (string dir in dirs)
                {
                    string sessionName = Path.GetFileName(dir);
                    if (!existService(sessionName))
                    {
                        if (deadSession(sessionName))
                        {
                            try
                            {
                                Directory.Delete(dir, true);
                            }
                            catch(Exception e)
                            {
                                // TO-DO: some reaction that alrady discarded session is probably used outside of this program
                            }
                        }
                    }  
                }

                // go thru sessions in memory and clean those that are no longer active
                foreach(KeyValuePair<string, WebVerifyServiceInfo> entry in _services)
                {
                    if(!entry.Value.isActive())
                    {
                        entry.Value.Clean();
                        removeService(entry.Key);
                    }
                }
            }
            // TO-DO: clean up those directory that has no active checker and no pending results to be viewed or timeout has passed
            //udelat pri startovani prochazku mezi vytvorenymi adresari (guid) a z toho vytvorit guid sessiony bez prirazeneho websocketu - nebo smazat, pokud uz dane info neni relevantni (stare, ci podle nejakeho souboru sessiony jiz nevyuzivane)
            //pri navazani spojeni se podivat, ejstli klient nepozaduje prirazeni k jiz existujicimu guid (ulozene asi v cookies) a podle toho mu to bud priradit, nebo mu rict sorry a priradit nove
            //i po odhlaseni websocketu bude checker dale aktivni, pokud tam neco probiha. pokud tam nic neprobiha a nic neni nacteno nebo neceka s vysledky an rpipojeni klienta, tak to sessionu smaze i s adresarem

        }

        private static bool deadSession(string sessionGuid)
        {
            // TO-DO: lookup config file of this session if it is already not used and finished
            return true;
        }

        public static async Task ProcessSocketRequest(AspNetWebSocketContext context)
        {
            WebVerifyServiceInfo wvsi = new WebVerifyServiceInfo(Guid.NewGuid(), context.WebSocket, checkerEventReaction);

            Directory.CreateDirectory(wvsi.guid.ToString());

            ///////////TEST////
            //File.Copy("BQT.results.zip", Path.Combine(wvsi.guid.ToString(), "BQT.results.zip"));
            /////////// END OF TEST////

            addService(wvsi);

            guidProposition(wvsi);

            wvsi.restartSocketTimeout();

            // maintain socket
            while (context.WebSocket.State == WebSocketState.Open)
            {
                var buffer = new byte[bufferSize];
                var offset = 0;
                var free = buffer.Length;
                while (true)
                {
                    WebSocketReceiveResult result = null;
                    try
                    {
                        result = await context.WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer, offset, free), CancellationToken.None);
                    }
                    catch (WebSocketException webSocketException)
                    {
                        if (webSocketException.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                        {
                            wvsi.socketDisconnected();
                            return;
                        }
                    }

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await context.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                        wvsi.socketDisconnected();
                        return;
                    }

                    wvsi.restartSocketTimeout();

                    offset += result.Count;
                    free -= result.Count;
                    if (result.EndOfMessage) break;
                    if (free == 0)
                    {
                        // No free spac
                        // Resize the outgoing buffer
                        var newSize = buffer.Length + bufferSize;
                        // Check if the new size exceeds a limit
                        // It should suit the data it receives
                        // This limit however has a max value of 2 billion bytes (2 GB)
                        if (newSize > 2000000)
                        {
                            throw new Exception("Maximum size exceeded");
                        }
                        var newBuffer = new byte[newSize];
                        Array.Copy(buffer, 0, newBuffer, 0, offset);
                        buffer = newBuffer;
                        free = buffer.Length - offset;
                    }
                }

                var stringMessage = System.Text.Encoding.UTF8.GetString(buffer).TrimEnd('\0');
                _ = Task.Run(() => {
                    try
                    {
                        if (context.WebSocket.State == WebSocketState.Open)
                        {
                            //var stringMessage = System.Text.Encoding.UTF8.GetString(buffer).TrimEnd('\0');

                            WSMessage wsMsg = JsonSerializer.Deserialize<WSMessage>(stringMessage);
                            switch (wsMsg.type)
                            {
                                case "keepAlive":
                                    // just to keep the connection active
                                    break;
                                case "startAnalyzing":
                                    wvsi.checker.StartVerification(false);
                                    break;
                                case "stopAnalyzing":
                                    wvsi.checker.cancelVerification();
                                    WSNotification notificationMsg = new WSNotification(VerificationNotificationType.verificationCanceled);
                                    wvsi.sendWSMessage(JsonSerializer.Serialize(notificationMsg));
                                    break;
                                case "startTestCasesGeneration":
                                    wvsi.checker.StartTestCasesGeneration();
                                    break;
                                case "stopTestCasesGeneration":
                                    wvsi.checker.cancelTestCasesGeneration();
                                    break;
                                case "importSystemArchiveFile":
                                    WSImportSystemArchiveFile wsISAF = JsonSerializer.Deserialize<WSImportSystemArchiveFile>(stringMessage);
                                    importSystemArchiveFile(wsISAF.fileName, wvsi);
                                    break;
                                case "loadRequirementsFromFile":
                                    WSLoadRequirementsFromFile wsLRFF = JsonSerializer.Deserialize<WSLoadRequirementsFromFile>(stringMessage);
                                    loadRequirementsFromFile(wsLRFF.fileName, wsLRFF.additionalFiles, wvsi);
                                    break;
                                case "loadRequirementsFromText":
                                    WSLoadRequirementsFromText wsLRFT = JsonSerializer.Deserialize<WSLoadRequirementsFromText>(stringMessage);
                                    if (File.Exists(wsLRFT.fileName))
                                    {
                                        File.Delete(wsLRFT.fileName);
                                    }
                                    File.WriteAllText(wsLRFT.fileName, wsLRFT.text);

                                    loadRequirementsFromText(wsLRFT.text, wsLRFT.fileName, wvsi);
                                    break;
                                case "getRequirements":
                                    getRequirements(wvsi);
                                    break;
                                case "getArchiveStructure":
                                    WSGetArchiveStructure wsGAS = JsonSerializer.Deserialize<WSGetArchiveStructure>(stringMessage);
                                    getArchiveStructure(wsGAS.fileName, wvsi);
                                    break;
                                case "saveFile":
                                    WSSaveFile wsSF = JsonSerializer.Deserialize<WSSaveFile>(stringMessage);
                                    WSFileSaved fileSavedMsg = new WSFileSaved(wsSF.fileName);
                                    try
                                    {
                                        if (File.Exists(wsSF.fileName))
                                        {
                                            File.Delete(wsSF.fileName);
                                        }
                                        File.WriteAllText(wsSF.fileName, wsSF.text);
                                    }
                                    catch (Exception e)
                                    {
                                        fileSavedMsg.errorMessage = e.Message;
                                    }
                                    wvsi.sendWSMessage(JsonSerializer.Serialize(fileSavedMsg));
                                    break;
                                case "guidRequest":
                                    WSGuidRequest wsGR = JsonSerializer.Deserialize<WSGuidRequest>(stringMessage);
                                    wvsi = guidRequested(wsGR.guid, wvsi);
                                    guidProposition(wvsi);
                                    break;
                                case "requestRequirementAdditionalHighlighting":
                                    WSRequestAdditionalHighlighting wsRAH = JsonSerializer.Deserialize<WSRequestAdditionalHighlighting>(stringMessage);
                                    additionalyHighlight(wsRAH, wvsi);
                                    break;
                                default:
                                    // non-recognized message type
                                    break;
                            }
                        }
                        else
                        {
                            // socket is closed
                        }
                    }
                    catch (ObjectDisposedException e)
                    {

                    }
                });
            }
        }

        private static void getRequirements(WebVerifyServiceInfo wvsi)
        {
            var list = new WSRequirementsList();

            list.reqs = wvsi.checker.systemModel.reqs.listOfRequirements();
            wvsi.sendWSMessage(JsonSerializer.Serialize(list));
        }

        private static void getArchiveStructure(string archiveName, WebVerifyServiceInfo wvsi)
        {
            var aStr = new WSArchiveStructure(archiveName);

            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(Path.Combine(wvsi.guid.ToString(), archiveName)))
                {
                    var separators = new char[] {
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar
                };

                    foreach (var entry in archive.Entries)
                    {
                        aStr.updateArchiveStructure(entry.FullName.Split(separators, StringSplitOptions.RemoveEmptyEntries), entry.Name.Length == 0);
                    }
                }

                wvsi.sendWSMessage(JsonSerializer.Serialize(aStr));
            }
            catch(Exception e)
            {
                // TO-DO: react on non existing files -> report back to client
            }
        }

        private static void loadRequirementsFromFile(string fileName, List<string> additionalFiles, WebVerifyServiceInfo wvsi)
        {
            if (additionalFiles.Count > 0)
            {
                wvsi.checker.importRequirementsFromFile(fileName, additionalFiles);
            }
            else
            {
                wvsi.checker.importRequirementsFromFile(fileName);
            }
            wvsi.checker.systemModel.FillUncoveredSignals();
            wvsi.checker.systemModel.UpdateInterfaceVariables();

            requirementsFromFileLoadedResponse(fileName, wvsi);
        }

        private static void loadRequirementsFromText(string text, string fileName, WebVerifyServiceInfo wvsi)
        {
            wvsi.checker.importRequirementsFromText(text, fileName);
            wvsi.checker.systemModel.FillUncoveredSignals();
            wvsi.checker.systemModel.UpdateInterfaceVariables();

            requirementsFromTextLoadedResponse(wvsi);
        }

        private static void importSystemArchiveFile(string fileName, WebVerifyServiceInfo wvsi)
        {
            try
            {
                wvsi.checker.importSystemArchiveFile(fileName);
            }
            catch (Exception e)
            {
                // TO-DO: remake it to error message
                WSWarning err = new WSWarning("Cannot import system archive file - " + e.Message, "ERROR");
                wvsi.sendWSMessage(JsonSerializer.Serialize(err));
            }
        }

        private static WebVerifyServiceInfo guidRequested(string guidString, WebVerifyServiceInfo wvsi)
        {
            // TO-DO: more sofisticated according to directory available and in future users associated with the guids
            if (guidString == wvsi.guid.ToString())
            {
                return wvsi;
            }
            else if (existService(guidString))
            {
                var storedWVSI = getService(guidString);
                //storedWVSI.socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Instance of this service was overtaken by new connection!", CancellationToken.None);
                //Directory.Move(storedWVSI.guid.ToString(), wvsi.guid.ToString
                wvsi.handOverSocketToDifferentServiceInfo(storedWVSI);
                //wvsi.socket = null;
                wvsi.Clean();
                removeService(wvsi.guid.ToString());

                return storedWVSI;
            }
            else
            {
                return wvsi;
            }
        }

        private static void guidProposition(WebVerifyServiceInfo wvsi)
        {
            StringBuilder json = new StringBuilder();

            json.AppendLine("{");
            json.AppendLine("\"type\": \"sessionGuidProposition\",");
            json.AppendLine("\"guid\": \"" + wvsi.guid.ToString() + "\"");
            json.AppendLine("}");

            wvsi.sendWSControlMessage(json.ToString());
        }

        private static void requirementsFromFileLoadedResponse(string fileName, WebVerifyServiceInfo wvsi)
        {
            StringBuilder json = new StringBuilder();

            json.AppendLine("{");
            json.AppendLine("\"type\": \"requirementsLoadedFromFile\",");
            json.AppendLine("\"file\": \"" + fileName + "\"");
            json.AppendLine("}");

            wvsi.sendWSMessage(json.ToString());
        }

        private static void requirementsFromTextLoadedResponse(WebVerifyServiceInfo wvsi)
        {
            wvsi.sendWSMessage("{\"type\": \"requirementsLoadedFromText\"}");
        }

        private static void sendWSmsg(WebSocket socket, string msg)
        {
            lock (socket)
            {
                socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg)), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        private static void checkerEventReaction(CheckerMessage msg, Checker checker, WebSocket socket)
        {
            switch (msg.type)
            {
                case CheckerMessageType.warning:
                    if (socket.State == WebSocketState.Open)
                    {
                        WSWarning warning = new WSWarning(((CheckerWarningMessage)msg).msg, ((CheckerWarningMessage)msg).title);
                        sendWSmsg(socket, JsonSerializer.Serialize(warning));
                    }
                    break;
                case CheckerMessageType.error:
                    if (socket.State == WebSocketState.Open)
                    {
                        // TO-DO: remake it to error message
                        WSWarning error = new WSWarning(((CheckerErrorMessage)msg).msg, "ERROR");
                        sendWSmsg(socket, JsonSerializer.Serialize(error));
                    }
                    break;
                case CheckerMessageType.newTestCasesRequestFile:
                    if (socket.State == WebSocketState.Open)
                    {
                        WSTestCasesRequestFile testCasesRequestFile = new WSTestCasesRequestFile(((CheckerNewTestCasesRequestFile)msg).file);
                        sendWSmsg(socket, JsonSerializer.Serialize(testCasesRequestFile));
                    }
                    break;
                case CheckerMessageType.newTestCases:
                    if (socket.State == WebSocketState.Open)
                    {
                        WSTestCases testCases = new WSTestCases();
                        var testCasesList = ((CheckerNewTestCases)msg).testCases;
                        for (int i = 0; i < testCasesList.Count; i++)
                        {
                            testCases.files.Add(testCasesList[i]);
                        }
                        sendWSmsg(socket, JsonSerializer.Serialize(testCases));
                    }
                    break;
                case CheckerMessageType.newVerificationResult:
                    Console.WriteLine("New data came from checker");
                    var updatedTable = UpdatedTab(((CheckerNewVerificationResult)msg).VRTable, ((CheckerNewVerificationResult)msg).VRTableDetails, ((CheckerNewVerificationResult)msg).metadata);
                    if (socket.State == WebSocketState.Open)
                    {
                        sendWSmsg(socket, updatedTable);
                    }
                    else
                    {
                        // socket is closed - reaction to unload checker etc.
                    }
                    break;
                case CheckerMessageType.CheckerVerificationNotification:
                    if (socket.State == WebSocketState.Open)
                    {
                        WSNotification notificationMsg = new WSNotification(((CheckerVerificationNotification)msg).notificationType);
                        sendWSmsg(socket, JsonSerializer.Serialize(notificationMsg));
                    }
                    else
                    {
                        // socket is closed - reaction to unload checker etc.
                    }
                    break;
                case CheckerMessageType.testCasesStatus:
                    if (socket.State == WebSocketState.Open)
                    {
                        WSTestCasesStatus testCasesStatus = new WSTestCasesStatus(((CheckerTestCasesStatus)msg).status);
                        sendWSmsg(socket, JsonSerializer.Serialize(testCasesStatus));
                    }
                    else
                    {
                        // socket is closed - reaction to unload checker etc.
                    }
                    break;
                default:
                    // MESSAGE THAT WE DO NOT RECOGNIZE OR DO NOT WANT
                    break;
            }
        }

        private static string UpdatedTab(DataTable VRTable, DataTable VRTableDetails, ResultsMetadata metadata)
        {
            StringBuilder json = new StringBuilder();

            json.AppendLine("{");
            json.AppendLine("\"type\": \"verificationResults\",");
            json.AppendLine("\"columns\": [");
            for (int i = 0; i < VRTable.Columns.Count; i++)
            {
                json.AppendLine("{");
                json.AppendLine("\"name\": \"" + VRTable.Columns[i].ToString().Replace("\"", "\\\"").Replace("\n", "").Replace("\r", "").Replace("\t", "\\t") + "\"");
                json.AppendLine("}" + ((i < VRTable.Columns.Count - 1) ? "," : ""));
            }
            json.AppendLine("],");

            json.AppendLine("\"rows\": [");
            for (int i = 0; i < VRTable.Rows.Count; i++)
            {
                if (VRTable.Rows[i].ItemArray.Length != VRTable.Columns.Count)
                {
                    throw new SystemException("Inconsistent length of columns in VRTable");
                }
                json.AppendLine("[");
                for (int j = 0; j < VRTable.Rows[i].ItemArray.Length; j++)
                {
                    json.AppendLine("{");
                    json.AppendLine("\"value\": \"" + VRTable.Rows[i].ItemArray[j].ToString().Replace("\"", "\\\"").Replace("\n", "").Replace("\r", "").Replace("\t", "\\t") + "\"");
                    if (metadata.flags[i][j] > 0)
                    {
                        json.AppendLine(", \"flags\": \"" + metadata.flags[i][j].ToString().Replace(" ", string.Empty).Replace(',', ' ') + "\"");
                    }
                    json.AppendLine("}" + ((j < VRTable.Rows[i].ItemArray.Length - 1) ? "," : ""));
                }
                json.AppendLine("]" + ((i < VRTable.Rows.Count - 1) ? "," : ""));
            }
            json.AppendLine("]");
            json.AppendLine("}");

            return json.ToString();
        }

        private static bool addService(WebVerifyServiceInfo wvsi)
        {
            lock(_serviceLock)
            {
                try
                {
                    _services.Add(wvsi.guid.ToString(), wvsi);
                }
                catch(Exception e)
                {
                    return false;
                }

                return true;
            }
        }

        private static bool removeService(string sessionGuid)
        {
            lock (_serviceLock)
            {
                return _services.Remove(sessionGuid);
            }
        }

        private static bool existService(string sessionGuid)
        {
            lock (_serviceLock)
            {
                 return _services.ContainsKey(sessionGuid);
            }
        }

        private static WebVerifyServiceInfo getService(string sessionGuid)
        {
            lock (_serviceLock)
            {
                try
                {
                    return _services[sessionGuid];
                }
                catch (Exception e)
                {
                    return null;
                }
            }
        }

        private static void additionalyHighlight(WSRequestAdditionalHighlighting rah, WebVerifyServiceInfo wvsi)
        {
            wvsi.sendWSMessage(JsonSerializer.Serialize(new WSadditionalHighlightingResponse(rah.guid, rah.hash, wvsi.checker.additionalHighlight(rah.text))));
        }


    }
}