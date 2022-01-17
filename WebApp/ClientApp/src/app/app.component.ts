import { Component, Renderer2, ViewChild } from '@angular/core';
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
import { ArchiveStructureTree } from './archiveStructureTree';
import { TestCaseFile } from './TestCaseFile';
import { htmlAstToRender3Ast } from '@angular/compiler/src/render3/r3_template_transform';
import { ɵInternalFormsSharedModule } from '@angular/forms';
import { timer } from 'rxjs';
import { stringify } from 'querystring';
import { FileInfo } from './file-info';
import { MultichoiceDialog } from './multichoice-dialog';
import { ClipsHighlighter } from './colorable-text-area/clips-highlighter/clips-highlighter';
import { EarsHighlighter } from './colorable-text-area/ears-highlighter/ears-highlighter'
import { HighlightItem } from './colorable-text-area/highlight-item';
import { TextHighlighter } from './colorable-text-area/text-highlighter';
import { v4 as uuidv4 } from 'uuid';
import { EditMemory } from './EditMemory';
import { TextChange } from './colorable-text-area/text-change';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.less']
})
export class AppComponent {
  static __defaultWorkAreaItemName : string = "Menu";
  static __reqAfterChangeTimeout : number = 2000;
  title = 'ClientApp';
  localFilePath : string = undefined;
  serverFilePath : string = undefined;
  sessionID : string = undefined;

  activeFile : string = "";
  activeFileChanged : boolean = false;
  analysisNotForCurrentItems : boolean = false;
  editableItemsName : string =  AppComponent.__defaultWorkAreaItemName;

  scrolledOnRequirements : boolean = true;
  scrolledOnResults : boolean = false;
  scrolledOnTestCases : boolean = false;

  analyzingNow : boolean = false;
  generatingTestCasesNow : boolean = false;
  uploadingTestCasesRequestFileNow : boolean = false;
  loadingRequirementsNow : boolean = false;

  generatingTestCasesTimer : NodeJS.Timeout = null;
  generatingTestCasesStart : Date = null;
  generatingTestCasesElapsedSeconds : number = null;

  static defaultTestCaseStatus : string = "waiting";

  testCasesStatus : string = AppComponent.defaultTestCaseStatus;

  fileViewVisible : boolean = false;
  fileViewContent : string = null;
  fileViewTitle : string = null;
  fileViewLoadingContent : boolean = false;

  mouseReqIndex : number = -1;

  verificationResults : VerificationResults = new VerificationResults();

  requirements : Array<Requirement> = [];

  testItems : Array<string> = [];

  @ViewChild('baa') buttonAnalyzeArea; 
  @ViewChild('saa') shadowAnalyzeArea; 

  @ViewChild('toust') toustArea;
  toustElement : HTMLElement;
  tousts : string[] = [];
  toustTimeout : NodeJS.Timeout = null;

  static toustTest : number = 0;

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
  public testCases : Array<TestCaseFile> = [];

  public testCasesRequestFile : TestCaseFile = null;

  @ViewChild('loginUserName') loginUserNameView; 
  @ViewChild('loginPassword') loginPasswordView;

  loginUserNameElement : HTMLInputElement;
  loginPasswordElement : HTMLInputElement;

  public loginScreenVisible : boolean = false;
  public loginUserNameError : string = null;
  public loginPasswordError : string = null;
  public logingIn : boolean = false;

  @ViewChild('documentNameInput') documentNameInputView; 
  @ViewChild('documentExtensionSelect') documentExtensionSelectView;

  public documentNameDialogVisible : boolean = false;
  public documentNameElement : HTMLInputElement;
  public documentExtensionElement : HTMLSelectElement;

  public documentNameError : string = null;

  // Make a variable reference to DialogType enum so it is targetable from template
  public _DialogType = DialogType;

  // public highlighter = null;
  public highlighter : TextHighlighter = null;
  public highlightingAfterTextChangeMethod : (text : string) => HighlightItem[] = null;

  private editMemory : EditMemory;
  private static UndoRedoReason : string = "UndoRedo";

  private static TextChangeCompositionTimout : number = 1000;

  private TextChangeComposition : {reqIdx : number, tch : TextChange, compositionTimeout : NodeJS.Timeout;} = {
    reqIdx : -1,
    tch : null,
    compositionTimeout : null
  }

  constructor(public _serverService : ServerService, private renderer:Renderer2)
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
    this._serverService.newTestCasesRequestFileEvent.subscribe((newFile : FileInfo) => {
      this.newTestCasesRequestFile(newFile);
    });
    this._serverService.newTestCasesEvent.subscribe((testCases : Array<FileInfo>) => {
      this.newTestCases(testCases);
    });
    this._serverService.archiveStructureEvent.subscribe((archiveStructure : any) => {
      this.newArchiveStructure(archiveStructure);
    });
    this._serverService.requirementAdditionalHighlightingEvent.subscribe((requirementAdditionalHighlighting : any) => {
      this.newRequirementAdditionalHighlighting(requirementAdditionalHighlighting);
    });
    this._serverService.testCasesStatusEvent.subscribe((status : string) => {
      this.newTestCasesStatus(status);
    });

    this.resetTextChangeComposition();
    this.editMemory = new EditMemory();

    document.addEventListener('keydown', (event) => {
      if (event.ctrlKey && (event.key === 'z' || event.key === 'Z')) {
        this.saveTextChangeComposition();
        console.log('Undo!', this.editMemory.undo());
      }
      if (event.ctrlKey && (event.key === 'y' || event.key === 'Y')) {
        this.saveTextChangeComposition();
        console.log('Redo!', this.editMemory.redo());
      }
    });

    window.onscroll = () => {this.scrolling()};
    window.onresize = () => {this.resizing()};
  }

  ngAfterViewInit() : void {
    this.setSizeOfAnalyzeAreas();
    this.workAreaElement = this.workAreaView.nativeElement;
    this.resultsAreaElement = this.resultsAreaView.nativeElement;  
    this.testCasesAreaElement = this.testCasesAreaView.nativeElement; 
    this.loginUserNameElement = <HTMLInputElement> this.loginUserNameView.nativeElement;
    this.loginPasswordElement = <HTMLInputElement> this.loginPasswordView.nativeElement;
    this.documentNameElement = <HTMLInputElement> this.documentNameInputView.nativeElement;
    this.documentExtensionElement = <HTMLSelectElement> this.documentExtensionSelectView.nativeElement;
    this.toustElement = this.toustArea.nativeElement;
    setTimeout(() => {
      this.resizing();
    });

    if(this._serverService.user == null)
    {
      this.newDialog(new MultichoiceDialog("No user is logged in. Do you want to log in or continue as guest?", "Note", [new DialogOption("Sign in", () => {
        this.loginScreenInvoke();
      }), new DialogOption("Continue", () => {
        this.newToust("You are in guest mode");
      })], 0));
    }
    else
    {
      this.newToust(`Logged as ${this._serverService.user}`);
    }
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
    if(this.requirements.length > 0 || this.testCasesRequestFile)
    {
      this.resetForNewAnalysis();
      this.analysisNotForCurrentItems = false;
      this.setSizeOfAnalyzeAreas();

      if(this.requirements.length > 0)
      {
        this._serverService.loadRequirementsFromText(this.reqsText, this.activeFile);
      }
      else if(this.testCasesRequestFile)
      {
        this._serverService.startTestCaseGeneration();
      }
    }   
  }

  public stopAnalyzeClick() : void {
    if(this.analyzingNow)
    {
      this._serverService.stopAnalyzing();
    }

    if(this.generatingTestCasesNow)
    {
      this._serverService.stopTestCaseGeneration();
    }
  }

  public createDocumentClick() : void {
    if(this.isDocumentNameFormatCorrect())
    {
      this.resetForNewRequirements();
      // TO DOOO 
      this.activeFile = this.documentNameElement.value + this.documentExtensionElement.value;
      this.chooseHighlighter(this.documentExtensionElement.value.slice(1));
      this.addRequirement(0);
      this.documentNameDialogDismiss();
    }
  }

  public documentNameDialogDismiss() : void {
    this.documentNameDialogVisible = false;
    this.documentNameDialogReset();
  }

  public documentNameChange() : void {
    this.isDocumentNameFormatCorrect();
  }

  public isDocumentNameFormatCorrect() : boolean {
    if(/\w+/.test(this.documentNameElement.value))
    {
      this.documentNameError = null;

      return true;
    }
    else
    {
      // TO-DO: better announcement
      this.documentNameError = "Document name is malformated!";

      return false;
    }
  }

  private documentNameDialogReset() : void {
    this.documentNameError = null;
    this.documentNameElement.value = "";
  }

  public userSignInClick() : void {
    
    if(this.isLoginUserNameFormatCorrect() && this.isLoginPasswordFormatCorrect())
    {
      // TO-DO: should do loading screen and then errors or modal saying that is is logged in as....
      this.logingIn = true;
      // TO-DO: really communicate to log in and to merge session on background and deal with the possibility of unsuccessfull log in
      setTimeout(() => {
        this._serverService.user = this.loginUserNameElement.value;
        this.logingIn = false;
        this.loginScreenDismiss();
      }, 2000);
    }
  }

  public logOut() : void {
    // TO-DO: more sophisticated
    this.newToust(`User ${this._serverService.user} signed off!`);
    this._serverService.user = null;
  }

  public loginScreenInvoke() : void {
    // TO-DO: more sophisticated
    this.loginScreenVisible = true;
  }

  private loginScreenReset() : void {
    this.loginPasswordError = null;
    this.loginUserNameError = null;
    this.loginUserNameElement.value = "";
    this.loginPasswordElement.value = "";
  }

  public loginScreenDismiss() : void {
    // TO-DO: more sophisticated
    this.loginScreenVisible = false;
    this.loginScreenReset();
    this.newToust(this._serverService.user == null?`You are in guest mode`:`Logged in as ${this._serverService.user}`);
  }

  public isLoginUserNameFormatCorrect() : boolean {
    // TO-DO - more sophisticated
    if(this.loginUserNameElement.value.length >= 4)
    {
      this.loginUserNameError = null;

      return true;
    }
    else
    {
      this.loginUserNameError = "User name is malformated!";

      return false; 
    }
  }

  public loginUserNameChange() : void {
    this.isLoginUserNameFormatCorrect();
  }

  public isLoginPasswordFormatCorrect() : boolean {
    if(this.loginPasswordElement.value.length >= 8)
    {
      this.loginPasswordError = null;

      return true;
    }
    else
    {
      // TO-DO: better announcement
      this.loginPasswordError = "Password is malformated!";

      return false;
    }
  }

  public loginPasswordChange() : void {
    this.isLoginPasswordFormatCorrect();
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

  private toustChanged() : void {
    if(this.toustTimeout == null && this.tousts.length > 0)
    {
      this.toustElement.innerHTML = this.tousts.shift();
      this.renderer.addClass(this.toustElement, "toustAnimate");
      this.toustTimeout = setTimeout(() => {
        this.renderer.removeClass(this.toustElement, "toustAnimate");
        this.toustTimeout = setTimeout(() => {
          this.toustTimeout = null;
          this.toustElement.innerHTML = "";
          this.toustChanged();
        }, 1000);
      }, 6000);
    }
  }

  private newToust(text : string) : void { 
    this.tousts.push(text);
    
    this.toustChanged();
  }

  private newTestCasesRequestFile(newFile : FileInfo) : void
  {
    this.testCasesRequestFile = new TestCaseFile(newFile);
    if(!this.testCasesRequestFile.isLoaded)
    {
      this._serverService.getArchiveStructure(this.testCasesRequestFile.fileName);
    }
  }

  private newTestCases(newTestcases : Array<FileInfo>) : void {
    this.testCases = [];
    var tempTestCases : Array<TestCaseFile> = new Array<TestCaseFile>();
    for(let testCase of newTestcases)
    {
      var newTestCase = new TestCaseFile(testCase);
      tempTestCases.push(newTestCase);
      if(!newTestCase.isLoaded)
      {
        this._serverService.getArchiveStructure(testCase.fileName);
      }
    }
    this.testCases = tempTestCases;
  }

  private updateAllReqs(newReqs : Array<string>) : void { 
    this.resetForNewRequirements();
    for(let req of newReqs)
    {
      let reqToPush : Requirement = new Requirement(uuidv4(), req.replace(/\n\r/g, "\n"));
      this.requirements.push(reqToPush);
      this.requirementChanged(reqToPush);
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
        this.generatingTestCasesNow = true;
        if(this.generatingTestCasesTimer)
        {
          clearInterval(this.generatingTestCasesTimer);
        }
        this.generatingTestCasesElapsedSeconds = 0;
        this.generatingTestCasesStart = new Date();
        this.generatingTestCasesTimer = setInterval(() => {
          this.generatingTestCasesElapsedSeconds = Math.round((new Date().getTime() - this.generatingTestCasesStart.getTime()) / 1000);
        }, 1000);
        break;
      case "testCasesCanceled":
      case "testCasesEnd":
        this.generatingTestCasesNow = false;

        if(this.generatingTestCasesTimer)
        {
          clearInterval(this.generatingTestCasesTimer);
          this.generatingTestCasesTimer = null;
        }

        // TO-DO: notify about analyzing finish
        break;
      default:
        // TO-DO: notify about unknown notification from server
        break;
    }
  }

  chooseHighlighter(extension : string) : void
  {
    switch(extension.toUpperCase())
    {
      case "CLP":
        this.editableItemsName = "Rules";
        this.highlighter = new ClipsHighlighter();
        break;
      case "EARS":
        this.editableItemsName = "Requirements";
        this.highlighter = new EarsHighlighter();
        break;
      default:
        this.editableItemsName = AppComponent.__defaultWorkAreaItemName;
        this.highlighter = null;
    }

    if(this.highlighter)
    {
      this.highlightingAfterTextChangeMethod = (text : string) => {
        return this.highlighter.generateHighlightedItems(text);
      }
    }
  }

  async uploadFile(files) {

    console.log('Upload file initialization', files);
    let fileNames : string[] = [];
    var extRE = /(?:\.([^.]+))?$/;
    var candidateActiveFile : string = null;
    var candidateSystemFile : string = null;
    for(let i = 0; i < files.length; i++)
    {
      var ext = extRE.exec(files[i].name)[1];
      switch(ext.toUpperCase())
      {
        case "CLP":
        case "EARS":
          if(candidateActiveFile)
          {
            this.newDialog(new WarningDialog("Cannot open more than one requirement or rule document at once!"));
            return;
          }
          candidateActiveFile = files[i].name;
          console.log("EXTENSION", ext);
          break;
        case "ZIP":
          if(candidateSystemFile)
          {
            this.newDialog(new WarningDialog("Cannot open more than one system archive at once!"));
            return;
          }
          candidateSystemFile = files[i].name;
          console.log("EXTENSION", ext);
          break;
        default:
          
      }
      fileNames.push(files[i].name);
    }

    // TO-DO: can i upload only additional files without the system and/or requirements file? If so, it should not reset the active and/or system files
    if(!candidateActiveFile && !candidateSystemFile)
    {
      this.newDialog(new WarningDialog("Cannot open documents without any rule, requirement or archive document!"));
      return;
    }

    if(candidateActiveFile)
    {
      var ext = extRE.exec(candidateActiveFile)[1];
      
      if(ext.toUpperCase() != "EARS" && ext.toUpperCase() != "CLP")
      {
        throw "Implementation mistake!"
      }

      this.chooseHighlighter(ext);
    }
    else
    {
      this.editableItemsName = AppComponent.__defaultWorkAreaItemName;
    }

    this.activeFile = candidateActiveFile?candidateActiveFile:"";

    let uploadFileInfo = { session: this._serverService.sessionGuid, fileNames: fileNames};
    let formData = new FormData();

    console.log('uploadFileInfo', uploadFileInfo);

    this.resetForNewRequirements();

    for(let i = 0; i < files.length; i++)
    {
      formData.append('files[]', files[i]);
    }
    formData.append("fileUploadInfo", JSON.stringify(uploadFileInfo));

    console.log('Sending', formData);
    this.loadingRequirementsNow = candidateActiveFile?true:false;
    this.uploadingTestCasesRequestFileNow = candidateSystemFile?true:false;
    
    try {
        let r = await fetch('/api/FileUpload', { method: "POST", body: formData });
        let returnedFileNames = JSON.parse(await r.json());
        console.log('HTTP responsee code:', r.status, returnedFileNames, typeof fileNames);
        if(r.status == 200 && returnedFileNames && returnedFileNames.fileNames && Array.isArray(returnedFileNames.fileNames))
        {
          var additionalFiles : string[] = [...returnedFileNames.fileNames];   
          if(candidateSystemFile)
          {
            if(returnedFileNames.fileNames.indexOf(candidateSystemFile) > -1)
            {
              additionalFiles.splice(returnedFileNames.fileNames.indexOf(candidateSystemFile),1);
              this._serverService.importSystemFile(candidateSystemFile);
            }
            else
            {
              this.uploadingTestCasesRequestFileNow = false;
              this.newDialog(new WarningDialog(`Response from server does not contain the system file : "${candidateSystemFile}"!`));
            }
          }

          if(this.activeFile.length > 0)
          {
            if(returnedFileNames.fileNames.indexOf(this.activeFile) > -1)
            {
              additionalFiles.splice(returnedFileNames.fileNames.indexOf(this.activeFile),1);
              this._serverService.loadRequirementsFromFile(this.activeFile, additionalFiles);
              this.activeFileChanged = false;
            }
            else
            {
              this.loadingRequirementsNow = false;
              this.newDialog(new WarningDialog(`Response from server does not contain the active file : "${this.activeFile}"!`));
            }  
          }
        }
        
    } catch (e) {
      this.loadingRequirementsNow = false;
      this.uploadingTestCasesRequestFileNow = false;
      this.newDialog(new WarningDialog(e.message));
    }
  }

  private setSizeOfAnalyzeAreas() : void
  {
    (<HTMLElement>this.shadowAnalyzeArea.nativeElement).style.height = (<HTMLElement>this.buttonAnalyzeArea.nativeElement).clientHeight.toString() + "px";
  }

  public requestAddRequirement(onPosition : number) : void {
    console.log(`Add requirement on ${onPosition} position`);
    this.saveTextChangeComposition();
    this.editMemory.new(
      () => {this.deleteRequirement(onPosition);},
      () => {this.addRequirement(onPosition);}
    );
    this.addRequirement(onPosition);
  }

  private addRequirement(onPosition : number) : void {
    this.requirements.splice(onPosition, 0, new Requirement(uuidv4(), ""));
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

  private deleteRequirement(reqIdx : number) : void {
    this.requirements.splice(reqIdx, 1);
    this.activeFileChanged = true;
    this.analysisNotForCurrentItems = true;
  }

  public requestDeleteRequirement(reqIdx : number) : void {
    console.log(`Request to delete requirement ${reqIdx}`);
    if(this.requirements[reqIdx].text.length == 0)
    {
      this.saveTextChangeComposition();
      this.editMemory.new(
        () => {this.addRequirement(reqIdx);},
        () => {this.deleteRequirement(reqIdx);}
      );
      this.deleteRequirement(reqIdx);
    }
    else
    {
      var CDia : ConfirmationDialog = new ConfirmationDialog(`Are you sure you want to delete ${reqIdx + 1}th item of ${this.editableItemsName}?`, 
      () => {
        let tch = new TextChange(0, this.requirements[reqIdx].text, "", AppComponent.UndoRedoReason);
        this.saveTextChangeComposition();
        this.editMemory.new(
          () => {
            this.addRequirement(reqIdx);
            setTimeout(() => {
              this.requirements[reqIdx].changeTextEvent.next(tch);
            });
          },
          () => {this.deleteRequirement(reqIdx);}
        );
        this.deleteRequirement(reqIdx);
      },
      () => {console.log(`Canceling the request to delete!`);});
      this.newDialog(CDia);
    }
  }

  public requirementChangedByUser(tch : TextChange, idx : number)
  {
    if(tch.reason != AppComponent.UndoRedoReason)
    {
      // if the change is only one character deletion or character addition -> it could be composed together -> otherwise not
      if((tch.newText.length == 1 && tch.prevText.length == 0) || (tch.newText.length == 0 && tch.prevText.length == 1))
      {
        // has to be from the same requirement in order to compose
        if(idx == this.TextChangeComposition.reqIdx)
        {
          // is it new character or erasing character?
          if(tch.newText.length == 1)
          {
            // can it be combined? does the newText follow up with what is saved?
            if(tch.idx == (this.TextChangeComposition.tch.idx + this.TextChangeComposition.tch.newText.length))
            {
              this.TextChangeComposition.tch.newText += tch.newText;
              this.restartTextChangeCompositionTimeout();
            }
            else
            {
              // cannnot be combined so start new composition
              this.saveTextChangeComposition();
              this.TextChangeComposition.reqIdx = idx;
              this.TextChangeComposition.tch = tch;
              this.restartTextChangeCompositionTimeout();
            }
          }
          else
          {
            // if deleting it can be backspace or deletion
            if(tch.idx == (this.TextChangeComposition.tch.idx + this.TextChangeComposition.tch.newText.length))
            {
              // delete
              this.TextChangeComposition.tch.prevText += tch.prevText;
              this.restartTextChangeCompositionTimeout();
            }
            else if(tch.idx == (this.TextChangeComposition.tch.idx + this.TextChangeComposition.tch.newText.length - 1))
            {
              // erase
              // is it erasing the newly added text or the  text already there before?
              if(this.TextChangeComposition.tch.newText.length > 0)
              {
                // erasing newly added text, so check if the characters match
                if(tch.prevText != this.TextChangeComposition.tch.newText.substr(this.TextChangeComposition.tch.newText.length - 1, 1))
                {
                  // problem -> the characters do not match up
                  throw "Characters deleted do not match up agains those that should be there!";
                }
                else
                {
                  // match -> let erase the last added character
                  this.TextChangeComposition.tch.newText = this.TextChangeComposition.tch.newText.slice(0, this.TextChangeComposition.tch.newText.length - 1);
                  this.restartTextChangeCompositionTimeout();
                }
              }
              else
              {
                // move the index and add to prevText
                this.TextChangeComposition.tch.idx = tch.idx;
                this.TextChangeComposition.tch.prevText = tch.prevText + this.TextChangeComposition.tch.prevText; 
                this.restartTextChangeCompositionTimeout();
              }
            }
            else
            {
              // cannnot be combined so start new composition
              this.saveTextChangeComposition();
              this.TextChangeComposition.reqIdx = idx;
              this.TextChangeComposition.tch = tch;
              this.restartTextChangeCompositionTimeout();
            }
          }
        }
        else
        {
          this.saveTextChangeComposition();
          this.TextChangeComposition.reqIdx = idx;
          this.TextChangeComposition.tch = tch;
          this.restartTextChangeCompositionTimeout();
        }
      }
      else
      {
        this.saveTextChangeComposition();
        let tchUndo : TextChange = new TextChange(tch.idx, tch.prevText, tch.newText, AppComponent.UndoRedoReason);
        let tchRedo : TextChange = new TextChange(tch.idx, tch.newText, tch.prevText, AppComponent.UndoRedoReason);
        this.editMemory.new(
          () => {this.requirements[idx].changeTextEvent.next(tchUndo);},
          () => {this.requirements[idx].changeTextEvent.next(tchRedo);}
        );
      }  
    }
    else
    {
      this.saveTextChangeComposition();
    }
    this.activeFileChanged = true;
    this.analysisNotForCurrentItems = true;
    this.requirementChanged(this.requirements[idx]);
  }

  private clearTextChangeCompositionTimout() : void
  {
    if(this.TextChangeComposition && this.TextChangeComposition.compositionTimeout)
    {
      clearTimeout(this.TextChangeComposition.compositionTimeout);
      this.TextChangeComposition.compositionTimeout = null;
    }
  }

  private restartTextChangeCompositionTimeout() : void
  {
    if(this.TextChangeComposition)
    {
      this.clearTextChangeCompositionTimout();
      this.TextChangeComposition.compositionTimeout = setTimeout(() => {
        this.saveTextChangeComposition();
      }, AppComponent.TextChangeCompositionTimout);
    }
  }

  private saveTextChangeComposition() : void
  {
    // console.log("saveTextChangeComposition");
    if(this.TextChangeComposition && this.TextChangeComposition.reqIdx >= 0 && this.TextChangeComposition.tch && (this.TextChangeComposition.tch.newText.length > 0 || this.TextChangeComposition.tch.prevText.length > 0))
    {
      // there is some TextChange to save
      let tchUndo : TextChange = new TextChange(this.TextChangeComposition.tch.idx, this.TextChangeComposition.tch.prevText, this.TextChangeComposition.tch.newText, AppComponent.UndoRedoReason);
      let tchRedo : TextChange = new TextChange(this.TextChangeComposition.tch.idx, this.TextChangeComposition.tch.newText, this.TextChangeComposition.tch.prevText, AppComponent.UndoRedoReason);
      // console.log("Saving manual edit undoes [reqIdx, idx, prevText, newText]", this.TextChangeComposition.reqIdx, this.TextChangeComposition.tch.idx, this.TextChangeComposition.tch.prevText, this.TextChangeComposition.tch.newText);
      let reqIdx = this.TextChangeComposition.reqIdx;
      this.editMemory.new(
        () => {this.requirements[reqIdx].changeTextEvent.next(tchUndo);},
        () => {this.requirements[reqIdx].changeTextEvent.next(tchRedo);}
      );
    }

    this.resetTextChangeComposition();
  }

  private resetTextChangeComposition() : void
  {
    this.clearTextChangeCompositionTimout();

    this.TextChangeComposition = {
      reqIdx : -1,
      tch : null,
      compositionTimeout : null};
  }

  public requirementChanged(req : Requirement) : void
  {
    req.highlights = this.highlighter.generateHighlightedItems(req.text);
    if(req.afterChangeAnalysisTimeout)
    {
      clearTimeout(req.afterChangeAnalysisTimeout);
    }
    req.afterChangeAnalysisTimeout = setTimeout(() => {
      if(req)
      {
        clearTimeout(req.afterChangeAnalysisTimeout);
        req.afterChangeAnalysisTimeout = null;
        if(this._serverService)
        {
          this._serverService.requestRequirementAdditionalHighlighting(req.ID, req.textHash(), req.text);
        }
      }
    }, AppComponent.__reqAfterChangeTimeout);
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
    var visiblePXs = [this.visibleWorkAreaPxs, this.visibleResultsAreaPxs, this.visibleTestCasesAreaPxs];

    let i = visiblePXs.indexOf(Math.max(...visiblePXs));

    this.scrolledOnRequirements = false;
    this.scrolledOnResults = false;
    this.scrolledOnTestCases = false;

    switch(i)
    {
      case 0:
        this.scrolledOnRequirements = true;
        break;
      case 1:
        this.scrolledOnResults = true;
        break;
      case 2:
        this.scrolledOnTestCases = true;
        break;
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
    this.testCasesStatus = AppComponent.defaultTestCaseStatus;
    if(this.generatingTestCasesTimer)
    {
      clearInterval(this.generatingTestCasesTimer);
    }

    this.generatingTestCasesTimer = null;
    this.generatingTestCasesStart = null;
    this.generatingTestCasesElapsedSeconds = null;
  }

  private resetTestCasesRequestFile() : void {
    this.testCasesRequestFile = null;
  }

  private resetResults() : void {
    this.verificationResults = new VerificationResults();
  }

  private resetForNewAnalysis() : void {
    this.resetResults();
    this.resetTestCases();
  }

  private resetForNewRequirements() : void {
    this.resetTextChangeComposition();
    this.editMemory.clearAllHistory();
    this.activeFileChanged = false;
    this.resetResults();
    this.resetTestCases();
    this.resetTestCasesRequestFile();
    this.resetRequirements();
  }

  public createFileClick() : void {
    if(this.requirements.length == 0 && this.testCasesRequestFile == null)
    {
      this.documentNameDialogVisible = true;
    }
    else
    {
      this.newDialog(new ConfirmationDialog('You already have open document. Are you sure you want to create a new one?',
      () => {
        console.log(`confirmed create new file`);
        this.documentNameDialogVisible = true;
      },
      () => {},
      'Document already open!',
      false,
      'Create new'));
    }
  }

  public uploadFileClick() : void {
    if(this.requirements.length == 0 && this.testCasesRequestFile == null)
    {
      (document.getElementById('uploadFileForm') as HTMLFormElement).reset();
      document.getElementById('image-file').click();
    }
    else
    {
      this.newDialog(new ConfirmationDialog('You already have open document. Are you sure you want to upload a new one?',
      () => {
        console.log(`confirmed upload`);
        (document.getElementById('uploadFileForm') as HTMLFormElement).reset();
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
      tempDownloadElement.setAttribute('href', '/api/FileDownload/?fileName=' + this.activeFile + '&session=' + this._serverService.sessionGuid);
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
        this.editableItemsName = AppComponent.__defaultWorkAreaItemName;
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
      this.editableItemsName = AppComponent.__defaultWorkAreaItemName;
      this.activeFileChanged = false;
      this.analysisNotForCurrentItems = true;
    }
  }

  private newArchiveStructure(archiveStructure : any)
  {
    
    if(archiveStructure && archiveStructure.fileName)
    {
      if(this.testCasesRequestFile && this.testCasesRequestFile.fileName == archiveStructure.fileName)
      {
        this.testCasesRequestFile.archiveStructure = new ArchiveStructureTree();
        this.testCasesRequestFile.archiveStructure.reloadFromArchiveTreeStructure(archiveStructure);
        this.testCasesRequestFile.isLoaded = true;
        this.uploadingTestCasesRequestFileNow = false;
      }

      if(this.testCases.length > 0)
      {
        var idx = this.testCases.findIndex(tc => tc.fileName == archiveStructure.fileName);

        if(idx >= 0)
        {
          this.testCases[idx].archiveStructure = new ArchiveStructureTree();
          this.testCases[idx].archiveStructure.reloadFromArchiveTreeStructure(archiveStructure);
          this.testCases[idx].isLoaded = true;
        }

        for(let tc of this.testCases)
        {
          if(!tc.isLoaded)
          {
            return;
          }
        }

        // TO-DO: signal that testcases are loaded
      }
    }
  }

  private newRequirementAdditionalHighlighting(requirementAdditionalHighlighting : any) : void
  {
    if(requirementAdditionalHighlighting && requirementAdditionalHighlighting.guid)
    {
      let req : Requirement = this.requirements.find(r => r.ID == requirementAdditionalHighlighting.guid);
      if(req && requirementAdditionalHighlighting.hash == req.textHash())
      {
        // TO-DO: add new highlights to the requirements highlights
        // let segments : HighlightItem[] = [];
        // segments.push(new HighlightItem(5,20, "ERROR"));
        this.highlighter.mergeLists(req.highlights, <HighlightItem[]>(requirementAdditionalHighlighting.highlights));
        req.highlights = [].concat(req.highlights); // to update the reference to array and so to trigger onChangein coloring ocmponent
      }
    }
  }

  private newTestCasesStatus(newStatus : string) : void
  {
    this.testCasesStatus = newStatus;
  }

  public openFileFromArchive(archiveName : string, filePath : string, forRow : number)
  {
    if(!this.fileViewVisible)
    {
      if(this.fileViewTitle != filePath)
      {
        this.fileViewLoadingContent = true;
        this.fileViewTitle = filePath;
        var iframe : HTMLIFrameElement = <HTMLIFrameElement>document.getElementById('fileViewIFrame');
        var iframedoc = iframe.contentDocument || iframe.contentWindow.document;
        iframedoc.body.innerHTML = "";

        console.log(`${filePath} from ${archiveName} archive trying ti retrive for ${forRow} row!`);
        this._serverService.getFileFromArchive(archiveName, filePath).subscribe(blob => {
          blob.text().then(s => {
            // console.log("The blob has content: ", s, blob);
            var extRE = /(?:\.([^.]+))?$/;
            var ext = extRE.exec(filePath)[1];
            switch(ext.toUpperCase())
            {
              case "HTM":
              case "HTML":
                this.fillFileViewAsHTML(s, filePath);
                break;
              case "XML":
                this.fillFileViewAsXML(s, filePath);
                break;
              case "CSV":
                this.fillFileViewAsCSV(s, filePath);
                break;
              default:
                this.fillFileViewAsText(s, filePath);
                break;
            }
          });
        });
      }
      this.fileViewVisible = true;
    }
  }

  public fillFileViewAsText(content : string, fileName : string)
  {
    this.fillFileView('<link rel="stylesheet" href="styles/hon-dls.min.css"><div style="white-space: pre-wrap">' + content + "</div>", fileName);
  }

  public fillFileViewAsHTML(content : string, fileName : string)
  {
    this.fillFileView(content, fileName);
  }

  public fillFileViewAsXML(content : string, fileName : string)
  {
    this.fillFileViewAsText(content.replace(/</g, "&lt").replace(/>/g,"&gt"), fileName);
  }

  public fillFileViewAsCSV(content : string, fileName : string)
  {
    var delimiter : string = ",";
    var lines : string[] = content.split(/\r\n|\r|\n/)
    var cells : string[][] = [];
    var maxCellsInLine : number = 0;

    for(let line of lines)
    {
      var lineCellsCount = 0;
      var lineCells : string[] = [];
      var insideQuotes : boolean = false;
      var cellStart : number = 0;
      var doubleQuotesDetected : boolean = false;
      var ignoreSegmentDetected : boolean = false;
      for(var i = 0; i < line.length; i++)
      {
        
        switch(line[i])
        {
          case '"':
            if(i < line.length - 1 && line[i + 1] == '"')
            {
              doubleQuotesDetected = true;
              i++;
              break;
            }
            else
            {
              insideQuotes = !insideQuotes;
              ignoreSegmentDetected = true;
            }
            break;
          case delimiter:
            if(!insideQuotes)
            {
              var newCell : string = line.substr(cellStart, i - cellStart);
              cellStart = i + 1;
              if(ignoreSegmentDetected)
              {
                newCell = newCell.replace(/(?<!")"(?!")/g, '');
              }
              if(doubleQuotesDetected)
              {
                newCell = newCell.replace(/""/g, '"');
              }

              lineCells.push(newCell);

              ignoreSegmentDetected = false;
              doubleQuotesDetected = false;
            }
            break;
          default:
            break;
        }
      }
      var newCell : string = line.substr(cellStart, i - cellStart);
      if(ignoreSegmentDetected)
      {
        newCell = newCell.replace(/(?<!")"(?!")/g, '');
      }
      if(doubleQuotesDetected)
      {
        newCell = newCell.replace(/""/g, '"');
      }

      lineCells.push(newCell);

      cells.push(lineCells);
      lineCellsCount++;
      maxCellsInLine = Math.max(maxCellsInLine, lineCellsCount);
    }

    var newContent : string = '<link rel="stylesheet" href="styles/hon-dls.min.css"><table class="table table-striped"><tbody>';

    for(let row = 0; row < cells.length; row++)
    {
      newContent += '<tr>';
      let col = 0;
      for(; col < cells[row].length; col++)
      {
        newContent += '<td>' + cells[row][col] + '</td>';
      }
      for(; col < maxCellsInLine; col++)
      {
        newContent += '<td></td';
      }
      newContent += '</tr>';
    }

    newContent += '</tbody></table>';

    this.fillFileView(newContent, fileName);
  }

  private fillFileView(content : string, fileName : string)
  {
    this.fileViewContent = content;
    this.fileViewTitle = fileName;

    var iframe : HTMLIFrameElement = <HTMLIFrameElement>document.getElementById('fileViewIFrame');
    var iframedoc = iframe.contentDocument || iframe.contentWindow.document;

    iframedoc.body.innerHTML = content;

    this.fileViewLoadingContent = false;
    this.fileViewVisible = true;
  }

  public closeFileView()
  {
    this.fileViewVisible = false;
    this.fileViewLoadingContent = false;
   }

  public test() : void {
    this.fillFileViewAsText("This is the test string!\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\nsdadasdasdasd", "file.test");
    // var d1 : WarningDialog = new WarningDialog("Test of warning");
    // this.newDialog(d1);
    // var d2 : ConfirmationDialog = new ConfirmationDialog(`Are you sure?`, 
    // () => {},
    // () => {});
    // this.newDialog(d2);
    // var d3 : Dialog = new Dialog("Test3", "title", DialogType.warning, [new DialogOption("option1"), new DialogOption("option2"), new DialogOption("option3")], 1);
    // this.newDialog(d3);
    // var d4 : Dialog = new Dialog("Test4", "title", DialogType.warning, [new DialogOption("option1"), new DialogOption("option2"), new DialogOption("option3"), new DialogOption("option4")], 3);
    // this.newDialog(d4);
    // var d5 : Dialog = new Dialog("Test5", "title", DialogType.warning, [new DialogOption("option1"), new DialogOption("option2"), new DialogOption("option3"), new DialogOption("option4"), new DialogOption("option5")], 0);
    // this.newDialog(d5);
  }

}
