import { Injectable } from '@angular/core';
import { Subject, Subscriber } from 'rxjs';
import { VerificationResultCell } from "./verification-result-cell"
import { VerificationResults } from "./verification-results"
import { WarningDialog } from "./warning-dialog"

@Injectable({
  providedIn: 'root'
})
export class ServerService {

    private _ws = null;

    public newReqsEvent : Subject<Array<string>> = new Subject<Array<string>>();

    public newResultsEvent : Subject<VerificationResults> = new Subject<VerificationResults>();

    public newNotificationEvent : Subject<string> = new Subject<string>();

    public newWarningEvent : Subject<WarningDialog> = new Subject<WarningDialog>();

    public newTestCasesEvent : Subject<Array<string>> = new Subject<Array<string>>();

    public fileSavedEvent : Subject<{fileName : string, errorMessage : string}> = new Subject<{fileName : string, errorMessage : string}>();

    // private _creatingFileOngoing : boolean = false;
    // private _callbackOnSuccessFileCreation : () => void = null;
    // private _callbackOnErrorFileCreation : (reason : string) => void = null;

    constructor() {
        console.log("Server service constructor");
    }

    public init(wsAddress : string) : void {
        this._ws = new WebSocket(wsAddress);

        this._ws.onmessage = (msg) => {this.messageFromWsServer(msg)};

        this._ws.onopen = () => {this.connectedToWsServer()};;
    }

    public startAnalyzing(): void {
        console.log("startAnalyzingFromFile");
        this._ws.send(JSON.stringify({type: "startAnalyzing"}));
    }

    public stopAnalyzing(): void {
        console.log("stopAnalyzingFromFile");
        this._ws.send(JSON.stringify({type: "stopAnalyzing"}));
    }

    // public createEmptyFile(fileName : string, callbackOnSuccess : () => void, callbackOnError : (reason : string) => void) : boolean {
    //     if(this._creatingFileOngoing)
    //     {
    //         return false;
    //     }
    //     else
    //     {
    //         this._callbackOnSuccessFileCreation = callbackOnSuccess;
    //         this._callbackOnErrorFileCreation = callbackOnError;
    //         this._ws.send(JSON.stringify({type: "createEmptyFile", fileName: fileName}));

    //         return true;
    //     }
    // }

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
            this._ws.send(JSON.stringify({type: "saveFile", text: text, fileName: fileName}));
        });
    }

    public loadRequirementsFromFile(fileName : string, additionalFiles : string[] = []) : void {
        console.log("loadRequirementsFromFile");
        this._ws.send(JSON.stringify({type: "loadRequirementsFromFile", fileName: fileName, additionalFiles: additionalFiles}));
    }

    /// TO-DO: fileName is for the backend so far to be able to distinguish the extensions and maybe more
    public loadRequirementsFromText(text : string, fileName : string) : void {
        console.log("loadRequirementsFromText");
        this._ws.send(JSON.stringify({type: "loadRequirementsFromText", text: text, fileName: fileName}));
    }

    private messageFromWsServer(msg : any) : void {
        console.log(msg);

        var message = JSON.parse(msg.data);
        switch (message.type) {
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
            case "testCases":
                console.log("testCases", message.testCases);
                this.newTestCasesEvent.next(message.testCases);
                break;
            case "fileSaved":
                console.log("fileSaved", message.fileName, message.errorMessage && message.errorMessage.length > 0?message.errorMessage:"successfully");
                this.fileSavedEvent.next({fileName: message.fileName, errorMessage: (message.errorMessage && message.errorMessage.length > 0)?message.errorMessage:null});
                break;
            // case "emptyFileCreated":
            //     console.log("emptyFileCreated", message.fileName);
            //     if(this._callbackOnSuccessFileCreation)
            //     {
            //         this._creatingFileOngoing = false;
            //         this._callbackOnSuccessFileCreation = null;
            //         this._callbackOnErrorFileCreation = null;
            //         this._callbackOnSuccessFileCreation();
            //     }
            //     break;
            // case "emptyFileCreatedError":
            //     console.log("emptyFileCreatedError", message.fileName, message.reason);
            //     if(this._callbackOnErrorFileCreation)
            //     {
            //         this._creatingFileOngoing = false;
            //         this._callbackOnSuccessFileCreation = null;
            //         this._callbackOnErrorFileCreation = null;
            //         this._callbackOnErrorFileCreation(message.reason);
            //     }
            //     break;
            default:
                throw `Not recognized websocket message from server: ${msg}`;
                break;

        }
    }

    private requestRequirementsList() : void {
        console.log("requestRequirementsList");
        this._ws.send(JSON.stringify({type: "getRequirements"}));
    }

    private newRequirementsList(reqs : Array<string>) {
        console.log("newRequirementsList");
        console.log(reqs);
        this.newReqsEvent.next(reqs);
    }

    private connectedToWsServer(): void {
        console.log("connection established");
        //this._ws.send(document.getElementById('StatusLabel').innerHTML);
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
// /// 
//         var newRow : Array<VerificationResultCell> = [];
//         for(let t=0; t < newData.columns.length; t++)
//         {
//             newRow.push(new VerificationResultCell("test", ["Realizable"]));
//             results.rows.push(newRow);
//         }
//         newRow = [];
//         for(let t=0; t < newData.columns.length; t++)
//         {
//             newRow.push(new VerificationResultCell("test", ["Realizable"]));
//             results.rows.push(newRow);
//         }
//         newRow = [];
//         for(let t=0; t < newData.columns.length; t++)
//         {
//             newRow.push(new VerificationResultCell("test", ["Violating"]));
//             results.rows.push(newRow);
//         }
//         newRow = [];
//         for(let t=0; t < newData.columns.length; t++)
//         {
//             newRow.push(new VerificationResultCell("test", ["Violating"]));
//             results.rows.push(newRow);
//         }
//         newRow = [];
//         for(let t=0; t < newData.columns.length; t++)
//         {
//             newRow.push(new VerificationResultCell("test", ["ViolatingNext"]));
//             results.rows.push(newRow);
//         }
//         newRow = [];
//         for(let t=0; t < newData.columns.length; t++)
//         {
//             newRow.push(new VerificationResultCell("test", ["ViolatingNext"]));
//             results.rows.push(newRow);
//         }
//         newRow = [];
//         for(let t=0; t < newData.columns.length; t++)
//         {
//             newRow.push(new VerificationResultCell("test", ["RootUnrealizability"]));
//             results.rows.push(newRow);
//         }
//         newRow = [];
//         for(let t=0; t < newData.columns.length; t++)
//         {
//             newRow.push(new VerificationResultCell("test", ["RootUnrealizability"]));
//             results.rows.push(newRow);
//         }
//         newRow = [];
//         for(let t=0; t < newData.columns.length; t++)
//         {
//             newRow.push(new VerificationResultCell("test", ["Redundant"]));
//             results.rows.push(newRow);
//         }
//         newRow = [];
//         for(let t=0; t < newData.columns.length; t++)
//         {
//             newRow.push(new VerificationResultCell("test", ["Redundant"]));
//             results.rows.push(newRow);
//         }
// ///

        this.newResultsEvent.next(results);
    }
}
