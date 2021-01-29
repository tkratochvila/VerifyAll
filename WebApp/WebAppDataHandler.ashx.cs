using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.WebSockets;
using System.Text.Json;
using InterLayerLib;
using System.Data;
using System.Text;
using System.IO;

namespace webApp
{
    /// <summary>
    /// Summary description for WebAppDataHandler
    /// </summary>
    public class WebAppDataHandler : IHttpHandler
    {
        private static int bufferSize = 1024;

        public void ProcessRequest(HttpContext context)
        {
            ProcessRequest(new HttpContextWrapper(context));
        }

        public void ProcessRequest(HttpContextBase context)
        {
            if (context.IsWebSocketRequest)
                context.AcceptWebSocketRequest(ProcessSocketRequest);
        }

        private async Task ProcessSocketRequest(AspNetWebSocketContext context)
        {
            var socket = context.WebSocket;

            Checker checker = new Checker();
            checker.subscribeEvents((msg) => { this.checkerEventReaction(msg, checker, socket); });

            // maintain socket
            while (true)
            { 
                var buffer = new byte[bufferSize];
                var offset = 0;
                var free = buffer.Length;
                while (true)
                {
                    var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, offset, free), CancellationToken.None);
                    offset += result.Count;
                    free -= result.Count;
                    if (result.EndOfMessage) break;
                    if (free == 0)
                    {
                        // No free space
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


                //var buffer = new ArraySegment<byte>(new byte[1024]);

                // async wait for a change in the socket
                //var result = await socket.ReceiveAsync(buffer, CancellationToken.None);

                _ = Task.Run(() => { 
                if (socket.State == WebSocketState.Open)
                    {
                        var stringMessage = System.Text.Encoding.UTF8.GetString(buffer).TrimEnd('\0');

                        WSMessage wsMsg = JsonSerializer.Deserialize<WSMessage>(stringMessage);
                        switch(wsMsg.type)
                        {
                            case "startAnalyzing":
                                checker.StartVerification(false);
                                break;
                            case "stopAnalyzing":
                                checker.cancelVerification();
                                WSNotification notificationMsg = new WSNotification(VerificationNotificationType.verificationCanceled);
                                sendWSmsg(socket, JsonSerializer.Serialize(notificationMsg));
                                break;
                            case "loadRequirementsFromFile":
                                WSLoadRequirementsFromFile wsLRFF = JsonSerializer.Deserialize<WSLoadRequirementsFromFile>(stringMessage);
                                loadRequirementsFromFile(wsLRFF.fileName, wsLRFF.additionalFiles, checker, socket);
                                break;
                            case "loadRequirementsFromText":
                                WSLoadRequirementsFromText wsLRFT = JsonSerializer.Deserialize<WSLoadRequirementsFromText>(stringMessage);
                                if (File.Exists(wsLRFT.fileName))
                                {
                                    File.Delete(wsLRFT.fileName);
                                }
                                File.WriteAllText(wsLRFT.fileName, wsLRFT.text);

                                loadRequirementsFromText(wsLRFT.text, wsLRFT.fileName, checker, socket);
                                break;
                            case "getRequirements":
                                getRequirements(checker, socket);
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
                                catch(Exception e)
                                {
                                    fileSavedMsg.errorMessage = e.Message;
                                }
                                sendWSmsg(socket, JsonSerializer.Serialize(fileSavedMsg));
                                break;
                        }             
                    }
                    else
                    {
                        // socket is closed
                    }
                });
            }
        }

        private void getRequirements(Checker checker, WebSocket socket)
        {
            var list = new WSRequirementsList();

            list.reqs = checker.systemModel.reqs.listOfRequirements();
            sendWSmsg(socket, JsonSerializer.Serialize(list));
        }

        private void loadRequirementsFromFile(string fileName, List<string> additionalFiles, Checker checker, WebSocket socket)
        {
            try
            {
                //checker.loadConfigs();
                checker.loadAutomationServers(AppDomain.CurrentDomain.BaseDirectory + "..\\configs\\AutomationServers.xml");
                checker.LoadVerificationToolCfg(AppDomain.CurrentDomain.BaseDirectory + "..\\configs\\VerificationTools.xml");
            }
            catch (WarningException ex)
            {
                // TO DO: report
            }
            catch (ErrorException ex)
            {
                // TO DO: report and close
            }

            if(additionalFiles.Count > 0)
            {
                checker.importRequirementsFromFile(fileName, additionalFiles);
            }
            else
            {
                checker.importRequirementsFromFile(fileName);
            }
            checker.systemModel.FillUncoveredSignals();
            checker.systemModel.UpdateInterfaceVariables();

            requirementsFromFileLoadedResponse(fileName, socket);
        }

        private void loadRequirementsFromText(string text, string fileName, Checker checker, WebSocket socket)
        {
            try
            {
                //checker.loadConfigs();
                checker.loadAutomationServers(AppDomain.CurrentDomain.BaseDirectory + "..\\configs\\AutomationServers.xml");
                checker.LoadVerificationToolCfg(AppDomain.CurrentDomain.BaseDirectory + "..\\configs\\VerificationTools.xml");
            }
            catch (WarningException ex)
            {
                // TO DO: report
            }
            catch (ErrorException ex)
            {
                // TO DO: report and close
            }

            checker.importRequirementsFromText(text, fileName);
            checker.systemModel.FillUncoveredSignals();
            checker.systemModel.UpdateInterfaceVariables();

            requirementsFromTextLoadedResponse(socket);
        }

        private void requirementsFromFileLoadedResponse(string fileName, WebSocket socket)
        {
            StringBuilder json = new StringBuilder();

            json.AppendLine("{");
            json.AppendLine("\"type\": \"requirementsLoadedFromFile\",");
            json.AppendLine("\"file\": \"" + fileName + "\"");
            json.AppendLine("}");

            sendWSmsg(socket, json.ToString());
        }

        private void requirementsFromTextLoadedResponse(WebSocket socket)
        {
             sendWSmsg(socket, "{\"type\": \"requirementsLoadedFromText\"}");
        }

        private void sendWSmsg(WebSocket socket, string msg)
        {
            lock (socket)
            {
                socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg)), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        private void checkerEventReaction(CheckerMessage msg, Checker checker, WebSocket socket)
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
                case CheckerMessageType.newTestCases:
                    if (socket.State == WebSocketState.Open)
                    {
                        WSTestCases testCases = new WSTestCases();
                        List<string> testCasesList = ((CheckerNewTestCases)msg).testCases;
                        for (int i = 0; i < testCasesList.Count; i++)
                        {
                            testCases.testCases.Add(testCasesList[i]);
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
                default:
                    // MESSAGE THAT WE DO NOT RECOGNIZE OR DO NOT WANT
                    break;
            }
        }

        private string UpdatedTab(DataTable VRTable, DataTable VRTableDetails, ResultsMetadata metadata)
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
                if(VRTable.Rows[i].ItemArray.Length != VRTable.Columns.Count)
                {
                    throw new SystemException("Inconsistent length of columns in VRTable");
                }
                json.AppendLine("[");
                for (int j = 0; j < VRTable.Rows[i].ItemArray.Length; j++)
                {
                    json.AppendLine("{");
                    json.AppendLine("\"value\": \"" + VRTable.Rows[i].ItemArray[j].ToString().Replace("\"", "\\\"").Replace("\n", "").Replace("\r", "").Replace("\t", "\\t") + "\"");
                    if(metadata.flags[i][j] > 0)
                    {
                        json.AppendLine(", \"flags\": \"" + metadata.flags[i][j].ToString().Replace(" ", string.Empty).Replace(',',' ') + "\"");
                    }
                    json.AppendLine("}" + ((j < VRTable.Rows[i].ItemArray.Length - 1) ? "," : ""));
                }
                json.AppendLine("]" + ((i < VRTable.Rows.Count - 1) ? "," : ""));
            }
            json.AppendLine("]");
            json.AppendLine("}");

            return json.ToString();

            //HTMLRenderer renderer = new HTMLRenderer(VRTable);
            //renderer.SetStyle();
            //renderer.DrawHeader();
            //return renderer.DrawRows();
        }

        public bool IsReusable
        {
            get
            {
                return false;
            }
        }
    }
}