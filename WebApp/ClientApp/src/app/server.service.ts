import { Injectable } from '@angular/core';
import { Subject, Subscriber, Observable } from 'rxjs';
import { VerificationResultCell } from "./verification-result-cell"
import { VerificationResults } from "./verification-results"
import { WarningDialog } from "./warning-dialog"
import { HttpClient } from "@angular/common/http";
import { CookieService } from 'ngx-cookie-service';

import { ArchiveStructureTree } from "./archiveStructureTree"
import { FileInfo } from './file-info';

@Injectable({
  providedIn: 'root'
})
export class ServerService {

    private _ws = null;

    public newReqsEvent : Subject<Array<string>> = new Subject<Array<string>>();

    public systemArchiveFileImportedEvent : Subject<string> = new Subject<string>();

    public newResultsEvent : Subject<VerificationResults> = new Subject<VerificationResults>();

    public newNotificationEvent : Subject<string> = new Subject<string>();

    public newWarningEvent : Subject<WarningDialog> = new Subject<WarningDialog>();

    public newTestCasesRequestFileEvent : Subject<FileInfo> = new Subject<FileInfo>();

    public newTestCasesEvent : Subject<Array<FileInfo>> = new Subject<Array<FileInfo>>();

    public fileSavedEvent : Subject<{fileName : string, errorMessage : string}> = new Subject<{fileName : string, errorMessage : string}>();

    public archiveStructureEvent : Subject<any> = new Subject<any>();

    public requirementAdditionalHighlightingEvent : Subject<any> = new Subject<any>();

    public testCasesStatusEvent : Subject<string> = new Subject<string>();

    private _sessionGuid : string = null;
    private _prefferedSessionGuid : string = null;

    private _connected : boolean = false;
    private _attemptingConnection : boolean = false;
    private _keepAliveTimeout : NodeJS.Timeout = null;
    private _wsAddress : string = null;
    private _wsMessageQue : Array<any> = [];

    private _user : string = null;
    
    // private _creatingFileOngoing : boolean = false;
    // private _callbackOnSuccessFileCreation : () => void = null;
    // private _callbackOnErrorFileCreation : (reason : string) => void = null;

    constructor(private http: HttpClient, private cookieService: CookieService) {
    }

    public init(wsAddress : string) : void {
        this._wsAddress = wsAddress;
        this._attemptingConnection = true;
        this.reconnect();
        this._user = this.cookieService.check('user')?this.cookieService.get('user'):null;
    }

    public get user() : string 
    {
        return this._user;
    }

    public set user(value : string)
    {
        if(value == null)
        {
            if(this.cookieService.check('user'))
            {
                this.cookieService.delete('user', '/');
            }
        }
        else
        {
            this.cookieService.set('user', value, 30, '/');
        }


        this._user = value;
    }

    public get prefferedSessionGuid() : string
    {
        return this._prefferedSessionGuid;
    }

    public set prefferedSessionGuid(guid : string)
    {
        // if(guid != this._prefferedSessionGuid)
        // {
        //     if(this._prefferedSessionGuid == null)
        //     {
        //         console.log("First set of preffered session guid");  
        //     }
        //     else
        //     {
        //         console.log("Session guid changing!");  
        //     }
        //     console.log(`Changing prefferedGuid from ${this._prefferedSessionGuid} to ${guid}`);
        // }

        this._prefferedSessionGuid = guid;
    }

    public get sessionGuid() : string
    {
        return this._sessionGuid;
    }

    private connectionLost() : void
    {
        if(!this._attemptingConnection)
        {
            console.log("connectionLost");
            this._connected = false;
            this._sessionGuid = null;
            if(this._keepAliveTimeout)
            {
                clearTimeout(this._keepAliveTimeout);
                this._keepAliveTimeout = null;
            }

            this._attemptingConnection = true;
            this.reconnect();
        }
    }

    private keepAlive() : void
    {
       this.sendWsMessage({type: "keepAlive"});
    //    this._ws.send(JSON.stringify({type: "keepAlive"}));
    //    this._ws.send(JSON.stringify({type: "test"}));
    }

    private sendWsControlMessage(data : any) : boolean
    {
        if (this._ws.readyState === WebSocket.OPEN) {
            this._ws.send(JSON.stringify(data));
            this.resetKeepAlive();
            return true;
        }
        else
        {
            this.connectionLost();
            return false;
        }
    }

    private sendWsMessage(data : any) : void
    {
        this._wsMessageQue.push(data);
        this.sendQuedMessages();
    }

    private sendQuedMessages() : void
    {
        if (this._ws.readyState === WebSocket.OPEN)
        {
            if(this._wsMessageQue.length > 0 && this.sessionGuid == this.prefferedSessionGuid)
            {
                try
                {
                    this._ws.send(JSON.stringify(this._wsMessageQue[0]));
                    this._wsMessageQue.shift();
                }
                catch
                {
                    // if it failed, it certainly fail the send, so just connectionLost will be enough, the shift has not gone thru so the message is still there to be sent
                    this.connectionLost();
                }
                this.resetKeepAlive();
                this.sendQuedMessages();    // send another one if any
            }
        }
        else
        {
            this.connectionLost();
        }
    }

    private connectedToWsServer(): void {
        console.log("connection established");
        this._connected = true;
        this.resetKeepAlive();
    }

    private resetKeepAlive() : void
    {
        // if(this._keepAliveTimeout)
        // {
        //     clearTimeout(this._keepAliveTimeout);
        //     this._keepAliveTimeout = null;
        // }

        this._keepAliveTimeout = setTimeout(() => {this.keepAlive()}, 10000);
    }

    private connect() : boolean
    {
        try
        {
            this._ws = new WebSocket(this._wsAddress);

            this._ws.onmessage = (msg) => {this.messageFromWsServer(msg)};

            this._ws.onopen = () => {this.connectedToWsServer()};

            this._ws.onclose = () => {
                this.connectionLost();
            };

            this._ws.onerror = () => {this.connectionLost();};
        }
        catch
        {
            return false;
        }
        return true;
    }

    private reconnect() : void
    {
        if (this._ws) {
            switch(this._ws.readyState)
            {
                case WebSocket.OPEN:
                    break;
                case WebSocket.CONNECTING:
                case WebSocket.CLOSING:
                    setTimeout(() => {this.reconnect()}, 1000);
                    break;
                case WebSocket.CLOSED:
                    if(this.connect())
                    {
                        this._attemptingConnection = false;
                    }
                    else
                    {
                        setTimeout(() => {this.reconnect()}, 10000);
                    }
                    break;
                default:
                    throw "unsuported websocket state!"
            }
            
        }
        else
        {
            if(this.connect())
            {
                this._attemptingConnection = false;
            }
            else
            {
                setTimeout(() => {this.reconnect()}, 10000);
            }
        }
    }

    public startAnalyzing(): void {
        console.log("startAnalyzingFromFile");
        this.sendWsMessage({type: "startAnalyzing"});
    }

    public stopAnalyzing(): void {
        console.log("stopAnalyzingFromFile");
        this.sendWsMessage({type: "stopAnalyzing"});
    }

    public startTestCaseGeneration(): void {
        console.log("startTestCasesGeneration");
        this.sendWsMessage({type: "startTestCasesGeneration"});
    }

    public stopTestCaseGeneration(): void {
        console.log("stopTestCasesGeneration");
        this.sendWsMessage({type: "stopTestCasesGeneration"});
    }

    public saveFileOnServer(text : string, fileName : string) : Promise<void> {
        console.log("saveFileOnServer");
        return new Promise<void>((resolve, reject) => {
            var sub = this.fileSavedEvent.subscribe((result) => {
                if(result && result.fileName && result.fileName == fileName)
                {
                    sub.unsubscribe();
                    if(result.errorMessage && result.errorMessage.length > 0)
                    {
                        reject(result.errorMessage);
                    }
                    else
                    {
                        resolve();
                    }            
                }            
            });
            this.sendWsMessage({type: "saveFile", text: text, fileName: fileName});
        });
    }

    public loadRequirementsFromFile(fileName : string, additionalFiles : string[] = []) : void {
        console.log("loadRequirementsFromFile");
        this.sendWsMessage({type: "loadRequirementsFromFile", fileName: fileName, additionalFiles: additionalFiles});
    }

    /// TO-DO: fileName is for the backend so far to be able to distinguish the extensions and maybe more
    public loadRequirementsFromText(text : string, fileName : string) : void {
        console.log("loadRequirementsFromText");
        this.sendWsMessage({type: "loadRequirementsFromText", text: text, fileName: fileName});
    }

    public importSystemFile(fileName : string) : void {
        console.log("importSystemFile");
        this.sendWsMessage({type: "importSystemArchiveFile", fileName: fileName});
    }

    private messageFromWsServer(msg : any) : void {
        var message = JSON.parse(msg.data);
        switch (message.type) {
            case "sessionGuidProposition":
                this.sessionGuidProposition(message.guid);
                break;
            case "verificationResults":
                this.newTableData(message);
                break;
            case "requirementsLoadedFromFile":
                this.requestRequirementsList();
                break;
            case "requirementsLoadedFromText":
                this.startAnalyzing();
                break;
            case "reqsList":
                this.newRequirementsList(message.reqs);
                console.log("reqs", message.reqs);
                break;
            case "notification":
                console.log("notification", message.notification);
                this.newNotificationEvent.next(message.notification);
                break;
            case "warning":
                console.log("warning", message.text, message.title);
                message.title?this.newWarningEvent.next(new WarningDialog(message.text, null, message.title)):this.newWarningEvent.next(new WarningDialog(message.text));
                break;
            case "testCasesRequestFile":
                console.log("testCasesRequestFile", message);
                this.newTestCasesRequestFile(message);
                break;
            case "testCases":
                console.log("testCases", message);
                this.newTestCases(message);
                break;
            case "fileSaved":
                console.log("fileSaved", message.fileName, message.errorMessage && message.errorMessage.length > 0?message.errorMessage:"successfully");
                this.fileSavedEvent.next({fileName: message.fileName, errorMessage: (message.errorMessage && message.errorMessage.length > 0)?message.errorMessage:null});
                break;
            case "archiveStructure":
                console.log("archiveStructure", message);
                this.archiveStructureEvent.next(message);      
                break;
            case "requirementAdditionalHighlightingResponse":
                // console.log("requirementAdditionalHighlightingResponse", message);
                this.requirementAdditionalHighlightingEvent.next(message);
                break;
            case "testCasesStatus":
                console.log("testCasesStatus", message);
                if(message.status)
                {
                    this.testCasesStatusEvent.next(message.status)
                }
                else
                {
                    throw `Not recognized websocket message from server: ${msg}`;
                }
                break;
            default:
                throw `Not recognized websocket message from server: ${msg}`;
                break;

        }
    }

    private sessionGuidRequest(guid : string) : void
    {
        console.log(`Requesting guid: ${guid}`);
        this.sendWsControlMessage({type: "guidRequest", guid: guid});
    }

    private sessionGuidProposition(newGuid : string) : void {
        console.log(`sessionGuidProposition: ${newGuid} with sessionGuid: ${this._sessionGuid} and preffered guid: ${this.prefferedSessionGuid} with ready state ${this._ws.readyState}`);
        // TO-DO: taking in account already setted guid when reconnecting or when using cookies ton remember session from last 

        // initial guid proposition after connection
        if(this._sessionGuid == null)
        {
            // whatever happens, we need to use this guid to communicate with server for now
            this._sessionGuid = newGuid;

            // already there is preffered guid -> so this is reconnection to existing
            if(this.prefferedSessionGuid)
            {
                // if proposed guid is not the preffered one (most certainly is not, since this is first gui proposition after reconnect) -> request the preffered one
                if(this.prefferedSessionGuid != newGuid)
                {
                    this.sessionGuidRequest(this.prefferedSessionGuid);
                }
            }
            // no preffered guid form before, so accept this one as preffered
            else
            {
                this.prefferedSessionGuid = newGuid;
                this.sendQuedMessages();
            }
        }
        else    // response after requested guid
        {
            // whatever happens, we need to use this guid to communicate with server for now
            this._sessionGuid = newGuid;

            // whether it matches or not, we need to accept it
            this.prefferedSessionGuid = newGuid;
            this.sendQuedMessages();
        }
    }

    private requestRequirementsList() : void {
        console.log("requestRequirementsList");
        this.sendWsMessage({type: "getRequirements"});
    }

    private newRequirementsList(reqs : Array<string>) {
        console.log("newRequirementsList");
        console.log(reqs);
        this.newReqsEvent.next(reqs);
    }

    private newTableData(newData : any) : void {
        
        var results : VerificationResults = new VerificationResults();      

        newData.columns.forEach((column) => {
            results.cols.push(column.name);
        });

        newData.rows.forEach((row) => {
           var newRow : Array<VerificationResultCell> = [];
            row.forEach((cell) => {     
                newRow.push(new VerificationResultCell(cell.value, cell.flags?cell.flags:undefined));
            });
            results.rows.push(newRow);
        });

        this.newResultsEvent.next(results);
    }

    public getArchiveStructure(fileName : string) : void {
        console.log("getArchiveStructure", fileName);
        this.sendWsMessage({type: "getArchiveStructure", fileName: fileName});
    }

    public getFileFromArchive(archiveName : string, filePath : string) : Observable<Blob>
    {
        var getReq : string = `/api/FileFromArchiveDownload/?archiveName=${archiveName.replace("/","%2F").replace("\\", "%5C")}&filePath=${filePath.replace("/","%2F").replace("\\", "%5C")}&session=${this.sessionGuid}`;
        return this.http.get(getReq, {responseType: 'blob'}); 
    }

    private newTestCasesRequestFile(newFile : any) : void {

        if(newFile && newFile.file && newFile.file.fileName)
        {
            this.newTestCasesRequestFileEvent.next(new FileInfo(newFile.file.fileName, newFile.file.info?newFile.file.info:null));
        }
        else
        {
            // TO-DO
        }
    }

    private newTestCases(testCases : any) : void {
        if(testCases && testCases.files && Array.isArray(testCases.files))
        {
            var tCases : Array<FileInfo> = [];

            for(let tc of testCases.files)
            {
                tCases.push(new FileInfo(tc.fileName, tc.info?tc.info:null));
            }

            this.newTestCasesEvent.next(tCases);
        }
        else
        {
            // TO-DO
        }
    }

    public requestRequirementAdditionalHighlighting(guid : string, hash : number, text : string)
    {
        this.sendWsMessage({type: "requestRequirementAdditionalHighlighting", guid: guid, hash : hash, text: text});
    }

}
