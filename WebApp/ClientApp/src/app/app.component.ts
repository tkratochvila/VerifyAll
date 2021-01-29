import { Component, ViewChild } from '@angular/core';
import { ServerService } from './server.service'
import { Requirement } from './requirement'
import { CaretHelper } from "./caret-helper"
import { AppModule } from './app.module';
import { VerificationResults } from './verification-results';
import { Dialog } from './dialog';
import { WarningDialog } from './warning-dialog';
import { ConfirmationDialog } from './confirmation-dialog'
import { DialogType } from './dialog-type'
import { DialogOption } from './dialog-option';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.less']
})
export class AppComponent {
  title = 'ClientApp';
  localFilePath : string = undefined;
  serverFilePath : string = undefined;
  sessionID : string = undefined;

  activeFile : string = "";
  activeFileChanged : boolean = false;
  analysisNotForCurrentItems : boolean = false;
  editableItemsName : string = "Items";

  scrolledOnRequirements : boolean = true;
  scrolledOnResults : boolean = false;
  scrolledOnTestCases : boolean = false;

  analyzingNow : boolean = false;
  testCasesGenerationInProgress : boolean = false;
  loadingRequirementsNow : boolean = false;

  mouseReqIndex : number = -1;

  verificationResults : VerificationResults = new VerificationResults();

  requirements : Array<Requirement> = [];

  testItems : Array<string> = [];

  @ViewChild('baa') buttonAnalyzeArea; 
  @ViewChild('saa') shadowAnalyzeArea; 

  @ViewChild('wa') workAreaView; 
  @ViewChild('ra') resultsAreaView; 
  @ViewChild('tca') testCasesAreaView; 
  workAreaElement : HTMLElement;
  resultsAreaElement : HTMLElement;
  testCasesAreaElement : HTMLElement;

  @ViewChild('mt') mainToolbarView; 
  @ViewChild('ws') workSpaceView;

  public navMenuHeight : number;
  public contentHeight : number;

  public activeDialogs : Array<Dialog> = [];
  public testCases : Array<string> = [];

  // Make a variable reference to DialogType enum so it is targetable from template
  public _DialogType = DialogType;

  constructor(private _serverService : ServerService)
  {
    this._serverService.init("wss://" + window.location.hostname + "/WebAppDataHandler.ashx");
    this._serverService.newReqsEvent.subscribe((newReqs : Array<string>) => {
      this.updateAllReqs(newReqs);
    });
    this._serverService.newResultsEvent.subscribe((newResults : VerificationResults) => {
      this.verificationResults = newResults;
    });
    this._serverService.newNotificationEvent.subscribe((notification : string) => {
      this.newNotification(notification);
    });
    this._serverService.newWarningEvent.subscribe((warning : WarningDialog) => {
      this.newDialog(warning);
    });
    this._serverService.newTestCasesEvent.subscribe((testCases : Array<string>) => {
      this.newTestCases(testCases);
    });

    window.onscroll = () => {this.scrolling()};
    window.onresize = () => {this.resizing()};
  }

  ngAfterViewInit() : void {
    this.setSizeOfAnalyzeAreas();
    this.workAreaElement = this.workAreaView.nativeElement;
    this.resultsAreaElement = this.resultsAreaView.nativeElement;  
    this.testCasesAreaElement = this.testCasesAreaView.nativeElement; 
    setTimeout(() => {
      this.resizing();
    });
  }

  public get reqsText() : string {
    var rText : string = "";
    for(let req of this.requirements)
    {
      rText += req.text + "\n\n";
    }
    return rText;
  }

  public analyzeClick() : void {
    this.resetForNewAnalysis();

    this._serverService.loadRequirementsFromText(this.reqsText, this.activeFile);
    this.analysisNotForCurrentItems = false;
    this.setSizeOfAnalyzeAreas();
  }

  public stopAnalyzeClick() : void {
    this._serverService.stopAnalyzing();
  }

  public activeDialogDismiss(idxPick : number) : void {
    if(this.activeDialogs.length > 0)
    {
      if(idxPick == -1)
      {
        if(this.activeDialogs[0].cancelCallback)
        {
          this.activeDialogs[0].cancelCallback();
        }
      }
      else if(idxPick >= 0)
      {
        if(this.activeDialogs[0].options[idxPick].callback)
        {
          this.activeDialogs[0].options[idxPick].callback();
        }
      }

      this.activeDialogs.shift();
    }
  }

  private newDialog(dialog : Dialog) : void {
    this.activeDialogs.push(dialog);
  }

  private newTestCases(newTestcases : Array<string>) : void {
    this.testCases = [];
    for(let testCase of newTestcases)
    {
      this.testCases.push(testCase);
    }
  }

  private updateAllReqs(newReqs : Array<string>) : void {
    this.requirements = [];
    for(let req of newReqs)
    {
      this.requirements.push(new Requirement(req.replace(/\n\r/g, "\n")));
    }
    this.loadingRequirementsNow = false;
  }

  private newNotification(notification : string) : void {
    switch(notification)
    {
      case "verificationStart":
        this.analyzingNow = true;
        break;
      case "verificationCanceled":
      case "verificationEnd":
        this.analyzingNow = false;
        // TO-DO: notify about analyzing finish
        break;
      case "testCasesStart":
        this.testCasesGenerationInProgress = true;
        break;
      case "testCasesCanceled":
      case "testCasesEnd":
        this.testCasesGenerationInProgress = false;
        // TO-DO: notify about analyzing finish
        break;
      default:
        // TO-DO: notify about unknown notification from server
        break;
    }
  }

  async uploadFile(files) {

    console.log('Upload file initialization', files);
    let fileNames : string[] = [];
    var extRE = /(?:\.([^.]+))?$/;
    var candidateActiveFile : string = null;
    for(let i = 0; i < files.length; i++)
    {
      var ext = extRE.exec(files[i].name)[1];
      switch(ext.toUpperCase())
      {
        case "CLP":
	case "CLEAR":
	case "ZIP":
          if(candidateActiveFile)
          {
            this.newDialog(new WarningDialog("Cannot open more than one requirement, rule or archive document at once!"));
            return;
          }
          candidateActiveFile = files[i].name;
          console.log("EXTENSION", ext);
          break;
        default:
          
      }
      fileNames.push(files[i].name);
    }

    if(!candidateActiveFile)
    {
      this.newDialog(new WarningDialog("Cannot open documents without any rule, requirement or archive document!"));
      return;
    }

    var ext = extRE.exec(candidateActiveFile)[1];
    
    switch(ext.toUpperCase())
      {
        case "CLP":
          this.editableItemsName = "Rules";
          break;
        case "CLEAR":
          this.editableItemsName = "Requirements";
	  break;
  	case "ZIP":
	  this.editableItemsName = "Archive";
          break;
        default:
          throw "Implementation mistake!"
      }

    this.activeFile = candidateActiveFile;
    let uploadFileInfo = { sessionKey: "testXXXkey", fileNames: fileNames};
    let formData = new FormData();

    console.log('uploadFileInfo', uploadFileInfo);

    this.resetForNewRequirements();

    for(let i = 0; i < files.length; i++)
    {
      formData.append('files[]', files[i]);
    }
    formData.append("fileUploadInfo", JSON.stringify(uploadFileInfo));

    console.log('Sending', formData);
    this.loadingRequirementsNow = true;
    try {
        let r = await fetch('/api/FileUpload', { method: "POST", body: formData });
        let returnedFileNames = JSON.parse(await r.json());
        console.log('HTTP responsee code:', r.status, returnedFileNames, typeof fileNames);
        if(r.status == 200 && returnedFileNames && returnedFileNames.fileNames && Array.isArray(returnedFileNames.fileNames) && returnedFileNames.fileNames.indexOf(this.activeFile) > -1)
        {
          var additionalFiles : string[] = [...returnedFileNames.fileNames];
          additionalFiles.splice(returnedFileNames.fileNames.indexOf(this.activeFile),1);
          this._serverService.loadRequirementsFromFile(this.activeFile, additionalFiles);
          this.activeFileChanged = false;
        }
        else
        {
          this.loadingRequirementsNow = false;
          this.newDialog(new WarningDialog(`Response from server does not contain the active file : "${this.activeFile}"!`));
          return;
        }
    } catch (e) {
      this.loadingRequirementsNow = false;
      this.newDialog(new WarningDialog(e.message));
    }
  }

  private setSizeOfAnalyzeAreas() : void
  {
    (<HTMLElement>this.shadowAnalyzeArea.nativeElement).style.height = (<HTMLElement>this.buttonAnalyzeArea.nativeElement).clientHeight.toString() + "px";
  }

  public addRequirement(onPosition : number) : void {
    console.log(`Add requirement on ${onPosition} position`);
    this.requirements.splice(onPosition, 0, new Requirement(""));
    this.activeFileChanged = true;
    this.analysisNotForCurrentItems = true;
    setTimeout(() => {this.requirements[onPosition].focusEvent.next();}, 0);
  }

  public mouseLeavedReqsTable() : void {
    this.mouseReqIndex = -1;
  }

  public mouseEnteredReqsTableRow(reqIdx : number) : void {
    this.mouseReqIndex = reqIdx;
  }

  public requestDeleteRequirement(reqIdx : number) : void {
    console.log(`Request to delete requirement ${reqIdx}`);
    if(this.requirements[reqIdx].text.length == 0)
    {
      this.requirements.splice(reqIdx, 1);
      this.activeFileChanged = true;
      this.analysisNotForCurrentItems = true;
    }
    else
    {
      var CDia : ConfirmationDialog = new ConfirmationDialog(`Are you sure you want to delete ${reqIdx + 1}th item of ${this.editableItemsName}?`, 
      () => {this.requirements.splice(reqIdx, 1);this.activeFileChanged = true;this.analysisNotForCurrentItems = true;},
      () => {console.log(`Canceling the request to delete!`);});
      this.newDialog(CDia);
    }
  }

  public requirementChangedByUser(idx : number)
  {
    this.activeFileChanged = true;
    this.analysisNotForCurrentItems = true;
    console.log(`req ${idx} changed`);
  }

  private scrolling() : void {
    this.decideScrollableFocus();
  }

  private resizing() : void {
    this.navMenuHeight = (<HTMLElement>this.mainToolbarView.nativeElement).getBoundingClientRect().height;
    (<HTMLElement>this.workSpaceView.nativeElement).style.top = this.navMenuHeight.toString() + "px";
    this.contentHeight = Math.max(document.documentElement.clientHeight || 0, window.innerHeight || 0) - this.navMenuHeight;
  }

  private get visibleWorkAreaPxs() : number {
    var br = this.workAreaElement.getBoundingClientRect();
    return Math.min(Math.max(br.height + (br.top - this.navMenuHeight), 0), this.contentHeight);
  }

  private get visibleResultsAreaPxs() : number {
    var br = this.resultsAreaElement.getBoundingClientRect();
    return (br.top < this.navMenuHeight)?Math.min(Math.max(br.height + (br.top - this.navMenuHeight), 0), this.contentHeight):Math.min(Math.max(this.contentHeight - (br.top - this.navMenuHeight), 0),br.height);
  }

  private get visibleTestCasesAreaPxs() : number {
    var br = this.testCasesAreaElement.getBoundingClientRect();
    return (br.top < this.navMenuHeight)?Math.min(Math.max(br.height + (br.top - this.navMenuHeight), 0), this.contentHeight):Math.min(Math.max(this.contentHeight - (br.top - this.navMenuHeight), 0),br.height);

  }

  private decideScrollableFocus() : void {
    if(this.visibleResultsAreaPxs > this.visibleWorkAreaPxs)
    {
      if(this.visibleResultsAreaPxs > this.visibleTestCasesAreaPxs)
      {
        this.scrolledOnRequirements = false;
        this.scrolledOnResults = true;
        this.scrolledOnTestCases = false;
      }
      else
      {
        this.scrolledOnRequirements = false;
        this.scrolledOnResults = false;
        this.scrolledOnTestCases = true;
      }
    }
    else if(this.visibleWorkAreaPxs > this.visibleTestCasesAreaPxs)
    {
      this.scrolledOnRequirements = true;
      this.scrolledOnResults = false;
      this.scrolledOnTestCases = false;
    }
    else
    {
      this.scrolledOnRequirements = false;
      this.scrolledOnResults = false;
      this.scrolledOnTestCases = true;
    }
  }

  public scrollToRequirements() : void {
    window.scrollTo(0, 0);
  }

  public scrollToResults() : void {
    window.scrollTo(0, this.workAreaElement.getBoundingClientRect().height);
  }

  public scrollToTestCases() : void {
    window.scrollTo(0, this.workAreaElement.getBoundingClientRect().height + this.resultsAreaElement.getBoundingClientRect().height);
  }

  private resetRequirements() : void {
    this.requirements = [];
  }

  private resetTestCases() : void {
    this.testCases = [];
  }

  private resetResults() : void {
    this.verificationResults = new VerificationResults();
  }

  private resetForNewAnalysis() : void {
    this.resetResults();
    this.resetTestCases();
  }

  private resetForNewRequirements() : void {
    this.resetResults();
    this.resetTestCases();
    this.resetRequirements();
  }

  public createFileClick() : void {
    this.resetForNewRequirements();
    this.activeFile="test.clear";
    this.addRequirement(0);
  }

  public uploadFileClick() : void {
    if(this.requirements.length == 0)
    {
      document.getElementById('image-file').click();
    }
    else
    {
      this.newDialog(new ConfirmationDialog('You already have open document. Are you sure you want to upload a new one?',
      () => {
        console.log(`confirmed upload`);
        document.getElementById('image-file').click();
      },
      () => {},
      'Document already open!',
      false,
      'Upload new'));
    }
  }

  public async downloadFileClick() {
    this._serverService.saveFileOnServer(this.reqsText, this.activeFile).then(() => {
      var tempDownloadElement = document.createElement('a');
      tempDownloadElement.setAttribute('href', '/api/FileDownload/?fileName=' + this.activeFile);
      tempDownloadElement.setAttribute('download', 'download');
      tempDownloadElement.click();
      this.activeFileChanged = false;
    }).catch((err) => {
      this.newDialog(new WarningDialog(err));
    });
  }

  public closeFileClick() {
    // TO-DO: do something with ongoing analysis?
    if(this.requirements.length != 0 && this.activeFileChanged)
    {
      this.newDialog(new ConfirmationDialog('You have changes that you have not downloaded yet. Are you sure you want to close the document?',
      () => {
        this.resetForNewRequirements();
        this.activeFile="";
        this.activeFileChanged = false;
        this.analysisNotForCurrentItems = true;
      },
      () => {},
      'Unsaved changes',
      false,
      'Close anyway'));
    }
    else
    {
      this.resetForNewRequirements();
      this.activeFile="";
      this.activeFileChanged = false;
      this.analysisNotForCurrentItems = true;
    }
  }

  public test() : void {
    var d1 : WarningDialog = new WarningDialog("Test of warning");
    this.newDialog(d1);
    var d2 : ConfirmationDialog = new ConfirmationDialog(`Are you sure?`, 
    () => {},
    () => {});
    this.newDialog(d2);
    var d3 : Dialog = new Dialog("Test3", "title", DialogType.warning, [new DialogOption("option1"), new DialogOption("option2"), new DialogOption("option3")], 1);
    this.newDialog(d3);
    var d4 : Dialog = new Dialog("Test4", "title", DialogType.warning, [new DialogOption("option1"), new DialogOption("option2"), new DialogOption("option3"), new DialogOption("option4")], 3);
    this.newDialog(d4);
    var d5 : Dialog = new Dialog("Test5", "title", DialogType.warning, [new DialogOption("option1"), new DialogOption("option2"), new DialogOption("option3"), new DialogOption("option4"), new DialogOption("option5")], 0);
    this.newDialog(d5);
  }
}
