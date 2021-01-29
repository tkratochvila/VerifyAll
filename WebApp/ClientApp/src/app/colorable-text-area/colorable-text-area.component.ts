import { Component, OnInit, ViewChild, Input, Output, EventEmitter, ChangeDetectorRef } from '@angular/core';
import { Subject } from 'rxjs';

class RequirementPart {
  text : string;
  class : string;
  lineBreak : boolean;

  constructor(reqText : string, reqPartClass : string = "normal", isItLineBreak : boolean = false)
  {
    this.text = reqText;
    this.class = reqPartClass;
    this.lineBreak = isItLineBreak;
  }
}

@Component({
  selector: 'app-colorable-text-area',
  templateUrl: './colorable-text-area.component.html',
  styleUrls: ['./colorable-text-area.component.less']
})

export class ColorableTextAreaComponent implements OnInit {
  @ViewChild('cta') viewChild; 

  private _textElement : HTMLElement;

  private _internalEnterRepresentation : string = "\n";

  private _text : string = "";

  private _viewInitialized : boolean = false;

  requirementParts : Array<RequirementPart> = [];

  @Output () userTextInput = new EventEmitter();

  constructor(private cdr: ChangeDetectorRef) { }

  ngOnInit(): void {
    // setInterval(() => {
    //   console.log("RP:", this.requirementParts);
    // },15000);
  }

  ngAfterViewInit() : void {
    this._textElement = this.viewChild.nativeElement;
    this._textElement.addEventListener("keydown", (event) => {
      if(event.keyCode==13){
        //enter key was pressed
        event.preventDefault();
        this._textElement.contentEditable = "false";
        var textRepre = this.text;
        var idx = this._caretPosition;
        this.text = textRepre.substr(0, idx) + this._internalEnterRepresentation + textRepre.substr(idx);
        setTimeout(() => {
          // console.log(`CI: ${idx.toString()}`);
          this._textElement.contentEditable = "true";
          this._caretPosition = idx + 1;
          this.userTextInput.emit();
          // console.log("CP: ", idx + 1, this._caretPosition);
        });
      }
      else if(event.keyCode==8) {
        //enter key was pressed
        event.preventDefault();
        var idx = this._caretPosition;
        if(idx > 0)
        {
          var textRepre = this.text;
          this._textElement.contentEditable = "false";
          this.text = textRepre.substr(0, idx - 1) + textRepre.substr(idx);
          setTimeout(() => {
            // console.log(`CI: ${idx.toString()}`);
            this._textElement.contentEditable = "true";
            this._caretPosition = idx - 1;
            this.userTextInput.emit();
            // console.log("CP: ", idx - 1, this._caretPosition);
          });
        }
      }
      else
      {
        // normal characters
        this.userTextInput.emit();
      }
    });
    this._viewInitialized = true;
    this.render();
  }

 
  get text() : string {
    return this._text;
  }

  @Input() set text(text : string) {
    // TBD: some sort of control of incoming text
    // console.log("Seting text to: ", text);

    this._text = text;

    this.textChange.emit(this._text);

    this.render();
  }

  @Output() textChange = new EventEmitter<string>();

  @Input() set focusEvent(focusEvent : Subject<void>) {
    // TBD: some sort of control of incoming text
    // console.log("Seting text to: ", text);

    focusEvent.subscribe(() => {this._caretPosition = 0;});
  }

  private get _caretPosition() : number {
    var caretOffset : number = 0;
    var doc = this._textElement.ownerDocument;
    var win = doc.defaultView;
    var sel;
    sel = win.getSelection();
    if (sel.rangeCount > 0) {
        var range : Range = <Range>(win.getSelection().getRangeAt(0));
        // console.log("range", range.endContainer, range.endContainer.parentElement);
        var selectedElement : HTMLElement = range.endContainer.parentElement;
        // console.log("Selected element in getPosition: ", selectedElement);
        for(var i = 0; i < this._textElement.childNodes.length; i++)
        {
          if(this._textElement.childNodes[i].nodeType == Node.ELEMENT_NODE)
          {
            if(selectedElement == <HTMLElement>this._textElement.childNodes[i])
            {
                caretOffset += range.endOffset;
                // console.log(`Found selected element as ${i}th children. Adding ${range.endOffset} to caretOffset which is: ${caretOffset}`);
                return caretOffset;
            }
            else
            {
                if((<HTMLElement>this._textElement.childNodes[i]).tagName == "BR")
                {
                    caretOffset++;
                    // console.log(`Found BR element. Adding 1 to caretOffset which is now: ${caretOffset}`);
                }
                else
                {
                    caretOffset += this._textElement.childNodes[i].textContent.length;
                    // console.log(`Found span element. Adding ${element.children[i].textContent.length} to caretOffset which is now: ${caretOffset}`);
                }
            }
          }
          else if(this._textElement.childNodes[i].nodeType == Node.TEXT_NODE)
          {
            if(selectedElement == this._textElement)
            {
              caretOffset += range.endOffset;
              // console.log(`Found selected element as ${i}th children. Adding ${range.endOffset} to caretOffset which is: ${caretOffset}`);
              return caretOffset;
            }
            else
            {
              caretOffset += this._textElement.childNodes[i].textContent.length;
            }
          }
        }
    }
    return 0;
  }

  private set _caretPosition(index : number) {
    var tempIdx = index;
    var i : number = 0;

    for(; i < this._textElement.childNodes.length; i++)
    {
        if(this._textElement.childNodes[i].nodeType == Node.ELEMENT_NODE)
        {
          var currentElement : HTMLElement = <HTMLElement>this._textElement.childNodes[i];

          if(currentElement.tagName == "BR")
          {
            // console.log("SCP: BR");
            // console.log("BR", tempIdx, tempIdx - 1);
            tempIdx = tempIdx==0?0:tempIdx - 1;
          }
          else
          {
            // console.log("SCP: SPAN", currentElement.textContent);
              if(currentElement.textContent.length >= tempIdx)
              {
                  // found the right element
                  for(let ni = 0; ni < currentElement.childNodes.length; ni++)
                  {
                    // console.log("THIS SPAN", tempIdx, this._textElement.childNodes[i].textContent);
                      if(currentElement.childNodes[ni].nodeType == Node.TEXT_NODE)
                      {
                          var range = document.createRange();
                          var sel = window.getSelection();
                          range.setStart(currentElement.childNodes[ni], tempIdx);
                          range.collapse(true);    
                          sel.removeAllRanges();
                          sel.addRange(range);
                          // console.log("SPAN-sel", sel);
                          return;
                      }
                  }
              }
              else
              {
                // console.log("SPAN", tempIdx, tempIdx - currentElement.textContent.length, currentElement.textContent);
                  tempIdx -= currentElement.textContent.length;
              }
          }
        }
        else if(this._textElement.childNodes[i].nodeType == Node.TEXT_NODE)
        {
          // console.log("SCP: ROOT TEXT NODE", this._textElement.childNodes[i].textContent);
          if(this._textElement.childNodes[i].textContent.length >= tempIdx)
          {
            // console.log("THIS DIV", tempIdx, this._textElement.childNodes[i].textContent);

            var range = document.createRange();
            var sel = window.getSelection();
            range.setStart(this._textElement.childNodes[i], tempIdx);
            range.collapse(true);    
            sel.removeAllRanges();
            sel.addRange(range);
            // console.log("TN-sel", sel);
            return;
          }
          else
          {
            // console.log("DIV", tempIdx, tempIdx - this._textElement.childNodes[i].textContent.length, this._textElement.childNodes[i].textContent);

            tempIdx -= this._textElement.childNodes[i].textContent.length;
          }
        }
    }
    // console.log("creating text node inside div");
    var newEmptyNode = document.createTextNode("");
    this._textElement.appendChild(newEmptyNode);

    var range = document.createRange();
    var sel = window.getSelection();
    range.setStart(newEmptyNode, 0);
    range.collapse(true);    
    sel.removeAllRanges();
    sel.addRange(range);   
    // console.log("newTN-sel", sel);
  }

  public userInput() : void {
    // console.log("userinput");
    if (this._textElement.contentEditable == "false")
    {
      console.log("Too fast typing - ignoring character!");
      return;
    }
    this._textElement.contentEditable = "false";

    var idx = this._caretPosition;
    this.text = this.textFromRender();  // to invoke re-render
    // console.log("tfr: ", this.text);
    setTimeout(() => {
      // console.log(`CI: ${idx.toString()}`);
      this._textElement.contentEditable = "true";
      this._caretPosition = idx;
      // console.log("CP: ", idx, this._caretPosition);
    });
  }

  private render() : void {
    if(this._viewInitialized)
    {
      // console.log("render", this._text);
      var textToRender : string = this._text;

      while (this.requirementParts.length > 0) {
        this.requirementParts.pop();
      }

      for(let child = this._textElement.firstChild; child !== null; child = child.nextSibling) {
        if(child.nodeType == Node.TEXT_NODE)
        {
          // console.log("deleting the text node");
          this._textElement.removeChild(child);
        }
      }
      
      var lastAddedWasBR = false;
      while(textToRender.length > 0)
      {
        // console.log("Text is before: ", textToRender);
        var idx : number = textToRender.indexOf(this._internalEnterRepresentation)
        // console.log("idx: ", idx);
        if(idx == 0)
        {
          // two be able to target cursor between empty lines just add empty span in between
          if(lastAddedWasBR)
          {
            // console.log("SPAN - Adding empty SPAN between BR");
            this.requirementParts.push(new RequirementPart(""));
          }

          // console.log("BR - Now there is new line here");
          this.requirementParts.push(new RequirementPart("","",true));
          textToRender = textToRender.replace(this._internalEnterRepresentation,"");


          lastAddedWasBR = true;
        }
        else if (idx > 0) {
          // here it should specify all sort of highlighting
          // console.log("SPAN - There is new line somewhere there");
          this.requirementParts.push(new RequirementPart(textToRender.substr(0, idx)));
          textToRender = textToRender.slice(idx); 
          lastAddedWasBR = false;
        }
        else
        {
          // console.log("SPAN - There is no new line till the end");
          this.requirementParts.push(new RequirementPart(textToRender));
          textToRender = ""; // end of the while, this is last element
          lastAddedWasBR = false;
        }
        // console.log("Text is after: ", textToRender);
      }

      // make sure the editable does not end with br element as the last line would be not targetable for seting the cursor
      if(lastAddedWasBR)
      {
        // console.log("BR - Fake last BR");
        this.requirementParts.push(new RequirementPart("","",true));
      }
      this.cdr.detectChanges();
    }
  }

  private textFromRender() : string {
    var text : string = "";

    for(var i = 0; i < this._textElement.childNodes.length; i++)
    {
      if(this._textElement.childNodes[i].nodeType == Node.ELEMENT_NODE)
      {
        if((<HTMLElement>this._textElement.childNodes[i]).tagName == "BR")
        {
          // console.log("TFR: BR");
          text += this._internalEnterRepresentation;
        }
        else
        {
          // console.log("TFR: SPAN: ", this._textElement.childNodes[i].textContent);
          text += this._textElement.childNodes[i].textContent;
        }
      }
      else if(this._textElement.childNodes[i].nodeType == Node.TEXT_NODE)
      {
        // console.log("TFR: TEXT in ROOT: ", this._textElement.childNodes[i].textContent);
        text += this._textElement.childNodes[i].textContent;
      }
    }

    return (text[text.length - 1] == this._internalEnterRepresentation)?text.substr(0, text.length - 1):text;
  }

  private createReqPartElement(text : string) : HTMLElement {
    var newSpan : HTMLElement = document.createElement("span");
    newSpan.className = "normal";
    newSpan.appendChild(document.createTextNode(text));

    return newSpan;
  }

  private createBrElement() : HTMLElement {
    return document.createElement("br");
  }



}
