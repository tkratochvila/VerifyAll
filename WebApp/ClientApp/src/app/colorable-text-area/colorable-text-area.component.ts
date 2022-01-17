import { Component, OnInit, ViewChild, Input, Output, EventEmitter, ChangeDetectorRef } from '@angular/core';
import { Subject } from 'rxjs';
import { HighlightItem } from './highlight-item';
import { TextChange } from './text-change';
import { CaretRange } from './caret-range'

class RequirementPart {
  text : string;
  class : string;
  lineBreak : boolean;
  comment : string;

  constructor(reqText : string, reqPartClass : string = "normal", comment : string = "", isItLineBreak : boolean = false)
  {
    this.text = reqText;
    this.class = "preWrap" + (reqPartClass.length > 0?" " + reqPartClass:"");
    this.lineBreak = isItLineBreak;
    this.comment = comment;
  }
}

enum HighlightIndexType {
  none,
  lineEnd,
  tab,
  partStart,
  partEnd
}

@Component({
  selector: 'app-colorable-text-area',
  templateUrl: './colorable-text-area.component.html',
  styleUrls: ['./colorable-text-area.component.less', './clips-highlighter/clips-highlighter-styles.less', './ears-highlighter/ears-highlighter-styles.less']
})

export class ColorableTextAreaComponent implements OnInit {
  @ViewChild('cta') viewChild; 
  @ViewChild('h') hintView;

  static __partHoverClass : string = "partHover";

  private _editable : boolean = false;
  private _editableNow : boolean = true;

  private _textElement : HTMLElement;

  private _hintElement : HTMLElement;

  private _internalEnterRepresentation : string = "\n";

  private _internalTabRepresentation : string = "\t";

  private _internalTabSubstitute : string = " ";
  private _internalTabClass : string = "colorableTextAreaTab";
  private _tabIndentation : number = 50;
  private _tabIndentationCoef : number = 1.5;

  private _text : string = "";

  private _viewInitialized : boolean = false;

  private _highlightingItems : HighlightItem[] = [];

  requirementParts : Array<RequirementPart> = [];

  @Output () userTextInput = new EventEmitter<TextChange>();

  constructor(private cdr: ChangeDetectorRef) { }

  ngOnInit(): void {
    // setInterval(() => {
    //   console.log("RP:", this.requirementParts);
    // },15000);
  }

  ngAfterViewInit() : void {
    this._textElement = this.viewChild.nativeElement;
    this._tabIndentation = Math.round(this._tabIndentationCoef * parseFloat(window.getComputedStyle(this._textElement).getPropertyValue('font-size')));
    this._hintElement = this.hintView.nativeElement;
    this._textElement.addEventListener("paste", (event) => {
      event.preventDefault();
      this.editableNow = false;
      var textRepre = this.text;
      var caret = this._caretPosition;
      if(caret.isValid)
      {
        var clipboardText = "";
        if (event.clipboardData && event.clipboardData.getData) {
          clipboardText = event.clipboardData.getData('text/plain');
        }
        this.text = textRepre.substr(0, caret.start) + clipboardText + textRepre.substr(caret.end);
        let tch : TextChange = new TextChange(caret.start, clipboardText, caret.isSelection?textRepre.substr(caret.start, caret.end - caret.start):"");
        setTimeout(() => {
          // console.log(`CI: ${idx.toString()}`);
          this.editableNow = true;
          this._caretPosition = new CaretRange(caret.start + clipboardText.length);
          this.userTextInput.emit(tch);
          // console.log("CP: ", idx + 1, this._caretPosition);
        });
      }
    });
    this._textElement.addEventListener("keydown", (event) => {
      if(event.key=="Enter"){
        //enter key was pressed
        event.preventDefault();
        this.editableNow = false;
        var textRepre = this.text;
        var caret = this._caretPosition;
        if(caret.isValid)
        {
          this.text = textRepre.substr(0, caret.start) + this._internalEnterRepresentation + textRepre.substr(caret.end);
          let tch : TextChange = new TextChange(caret.start, this._internalEnterRepresentation);
          setTimeout(() => {
            // console.log(`CI: ${idx.toString()}`);
            this.editableNow = true;
            this._caretPosition = new CaretRange(caret.start + 1);
            this.userTextInput.emit(tch);
            // console.log("CP: ", idx + 1, this._caretPosition);
          });
        }
      }
      else if(event.key=="Tab"){
        //tab key was pressed
        event.preventDefault();
        this.editableNow = false;
        var textRepre = this.text;
        var caret = this._caretPosition;
        if(caret.isValid)
        {
          this.text = textRepre.substr(0, caret.start) + this._internalTabRepresentation + textRepre.substr(caret.end);
          let tch : TextChange = new TextChange(caret.start, this._internalTabRepresentation);
          setTimeout(() => {
            this.editableNow = true;
            this._caretPosition = new CaretRange(caret.start + 1);
            this.userTextInput.emit(tch);
          });
        }
      }
      else if(event.key=="Backspace") {
        //Backspace key was pressed
        event.preventDefault();
        var caret = this._caretPosition;
        if(caret.isValid && caret.end > 0)
        {
          if(caret.isSelection() || (caret.start > 0))
          {
            var textRepre = this.text;
            this.editableNow = false;
            var newTextRepre = textRepre.substr(0, caret.start - (caret.isSelection()?0:1)) + textRepre.substr(caret.end);
            this.text = newTextRepre;
            let tch : TextChange = caret.isSelection()?new TextChange(caret.start, "", textRepre.substr(caret.start, caret.end - caret.start)):new TextChange(caret.start - 1, "", textRepre.substr(caret.start - 1, 1));
            setTimeout(() => {
              // console.log(`CI: ${idx.toString()}`);
              this.editableNow = true;
              this._caretPosition = new CaretRange(caret.start - (caret.isSelection()?0:1));
              this.userTextInput.emit(tch);
              // console.log("CP: ", idx - 1, this._caretPosition);
            });
          }
        }
      }
      else if(event.key=="Delete") {
        //Delete key was pressed
        event.preventDefault();
        var caret = this._caretPosition;
        if(caret.isValid && caret.end > 0)
        {
          var textRepre = this.text;
          if(caret.isSelection() || (caret.end < textRepre.length))
          {
            this.editableNow = false;
            var newTextRepre = textRepre.substr(0, caret.start) + textRepre.substr(caret.end + (caret.isSelection()?0:1));
            this.text = newTextRepre;
            let tch : TextChange = caret.isSelection()?new TextChange(caret.start, "", textRepre.substr(caret.start, caret.end - caret.start)):new TextChange(caret.start, "", textRepre.substr(caret.start, 1));
            setTimeout(() => {
              // console.log(`CI: ${idx.toString()}`);
              this.editableNow = true;
              this._caretPosition = new CaretRange(caret.start);
              this.userTextInput.emit(tch);
              // console.log("CP: ", idx - 1, this._caretPosition);
            });
          }
        }
      }
      else if(event.key=="Control")
      {
        // do nothing to allow to use ctrl+c...if something would be done then when rendering the selection would be lost
      }
      else
      {
        var caret = this._caretPosition;
        if(caret.isValid && event.key.length == 1 && !event.ctrlKey)
        {
          // normal characters
          let tch : TextChange = new TextChange(caret.start, event.key);
          setTimeout(() => {
            this.userTextInput.emit(tch);
          });
        }
      }
    });
    this._viewInitialized = true;
    this.render();
  }

  // if prevText is specified, it controls, whether the text matches
  @Input() set changeTextEvent(textChangeEvent : Subject<TextChange>)
  {
    textChangeEvent.subscribe((textChange : TextChange) => {
      if(this.editable)
      {
        let renderedText : string = this.text;
        this.editableNow = false;

        if(textChange.prevText.length > 0)
        {
          if(renderedText.length >= (textChange.prevText.length + textChange.idx))
          {
            if (textChange.prevText != renderedText.substr(textChange.idx, textChange.prevText.length))
            {
              this.editableNow = true;
              return false;
            }
          }
          else
          {
            // missmatch with prevText
            this.editableNow = true;
            return false;
          }
        }

        let text1 : string = renderedText.substr(0, textChange.idx);
        let text2 : string = (textChange.newText.length > 0)?textChange.newText:"";
        let text3 : string = renderedText.substr(textChange.idx + textChange.prevText.length);
        this.text = text1 + text2 + text3;

        // this.text = renderedText.substr(0, textChange.idx) + (textChange.newText.length > 0)?textChange.newText:"" + renderedText.substr(textChange.idx + textChange.prevText.length);

        setTimeout(() => {
          this.editableNow = true;
          this._caretPosition = new CaretRange(textChange.idx + textChange.newText.length);
          this.userTextInput.emit(textChange);
        });

        return true;
      }
      else
      {
        return false;
      }
    });
  }

  get editable() : boolean {
    return this._editable;
  }

  @Input() set editable(value : boolean) {
    this._editable = value;
  }

  get editableNow() : boolean {
    return this._editableNow;
  }

 set editableNow(value : boolean) {
    this._editableNow = value;
    this.cdr.detectChanges();
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

  @Input() set highlightingItems(hItems : HighlightItem[])
  {
    this._highlightingItems = hItems;
    this.render();
  }

  @Output() textChange = new EventEmitter<string>();

  @Input() set focusEvent(focusEvent : Subject<void>) {
    // TBD: some sort of control of incoming text
    // console.log("Seting text to: ", text);

    focusEvent.subscribe(() => {this._caretPosition = new CaretRange(0);});
  }

  private get _caretPosition() : CaretRange {
    var caretOffset : number = 0;
    var doc = this._textElement.ownerDocument;
    var win = doc.defaultView;
    var sel;
    sel = win.getSelection();
    if (sel.rangeCount > 0) {
      var range : Range = <Range>(win.getSelection().getRangeAt(0));

      var childIdx = 0;
      var startIdx : number = 0;
      var endIdx : number = 0;

      if(range.startContainer.nodeType == Node.ELEMENT_NODE && <HTMLElement>range.startContainer == this._textElement)
      {
        for(;childIdx < range.startOffset; childIdx++)  // the node that it starts with cannot be counted as it needs the start of this node in selection (thats why < is used)
        {
          if(this._textElement.childNodes[childIdx].nodeType == Node.TEXT_NODE)
          {
            caretOffset += this._textElement.childNodes[childIdx].textContent.length;
          }
          else if(this._textElement.childNodes[childIdx].nodeType == Node.ELEMENT_NODE)
          {
            if((<HTMLElement>this._textElement.childNodes[childIdx]).tagName == "BR")
            {
              caretOffset++;
            }
            else
            {
              caretOffset += this._textElement.childNodes[childIdx].textContent.length;
            }
          }
        }
        startIdx = caretOffset;
        childIdx--; // to move it back to same index for the looking for end that could be in the same place
      }
      else
      {
        var selectedStartElement : HTMLElement = range.startContainer.parentElement;
        for(;childIdx < this._textElement.childNodes.length; childIdx++)
        {
          if(this._textElement.childNodes[childIdx].nodeType == Node.TEXT_NODE)
          {
            if(selectedStartElement == this._textElement)
            {
              startIdx = caretOffset + range.startOffset;
              break;
            }
            else
            {
              caretOffset += this._textElement.childNodes[childIdx].textContent.length;
            }
          }
          else if(this._textElement.childNodes[childIdx].nodeType == Node.ELEMENT_NODE)
          {
            if(selectedStartElement == <HTMLElement>this._textElement.childNodes[childIdx])
            {
              startIdx = caretOffset + range.startOffset;
              break;
            }
            else if((<HTMLElement>this._textElement.childNodes[childIdx]).tagName == "BR")
            {
              caretOffset++;
            }
            else
            {
              caretOffset += this._textElement.childNodes[childIdx].textContent.length;
            }
          }
        }
      }

      if(range.endContainer.nodeType == Node.ELEMENT_NODE && <HTMLElement>range.endContainer == this._textElement)
      {
        for(;childIdx < range.endOffset; childIdx++) // the node that it ends with has to be counted as it needs the end of this node in selection (thats why <= is used)
        {
          if(this._textElement.childNodes[childIdx].nodeType == Node.TEXT_NODE)
          {
            caretOffset += this._textElement.childNodes[childIdx].textContent.length;
          }
          else if(this._textElement.childNodes[childIdx].nodeType == Node.ELEMENT_NODE)
          {
            if((<HTMLElement>this._textElement.childNodes[childIdx]).tagName == "BR")
            {
              caretOffset++;
            }
            else
            {
              caretOffset += this._textElement.childNodes[childIdx].textContent.length;
            }
          }
        }
        endIdx = caretOffset;
      }
      else
      {
        var selectedEndElement : HTMLElement = range.endContainer.parentElement;
        for(;childIdx < this._textElement.childNodes.length; childIdx++)
        {
          if(this._textElement.childNodes[childIdx].nodeType == Node.TEXT_NODE)
          {
            if(selectedEndElement == this._textElement)
            {
              endIdx = caretOffset + range.endOffset;
              break;
            }
            else
            {
              caretOffset += this._textElement.childNodes[childIdx].textContent.length;
            }
          }
          else if(this._textElement.childNodes[childIdx].nodeType == Node.ELEMENT_NODE)
          {
            if(selectedEndElement == <HTMLElement>this._textElement.childNodes[childIdx])
            {
              endIdx = caretOffset + range.endOffset;
              break;
            }
            else if((<HTMLElement>this._textElement.childNodes[childIdx]).tagName == "BR")
            {
              caretOffset++;
            }
            else
            {
              caretOffset += this._textElement.childNodes[childIdx].textContent.length;
            }
          }
        }
      }
      return new CaretRange(startIdx, endIdx);
    }
    return new CaretRange(0, 0);
  }

  private set _caretPosition(caret : CaretRange) {
    var tempCaretStart = caret.start;
    var i : number = 0;
    var lookingForStart : boolean = true;

    var range = document.createRange();

    for(; i < this._textElement.childNodes.length; i++)
    {
        if(this._textElement.childNodes[i].nodeType == Node.ELEMENT_NODE)
        {
          var currentElement : HTMLElement = <HTMLElement>this._textElement.childNodes[i];

          if(currentElement.tagName == "BR")
          {
            tempCaretStart = tempCaretStart==0?0:tempCaretStart - 1;
          }
          else
          {
              if(currentElement.textContent.length >= tempCaretStart)
              {
                  // found the right element
                  for(let ni = 0; ni < currentElement.childNodes.length; ni++)
                  {
                      if(currentElement.childNodes[ni].nodeType == Node.TEXT_NODE)
                      {
                          range.setStart(currentElement.childNodes[ni], tempCaretStart);
                          lookingForStart = false;
                          break;
                      }
                  }
              }
              else
              {
                  tempCaretStart -= currentElement.textContent.length;
              }
          }
        }
        else if(this._textElement.childNodes[i].nodeType == Node.TEXT_NODE)
        {
          if(this._textElement.childNodes[i].textContent.length >= tempCaretStart)
          {
            range.setStart(this._textElement.childNodes[i], tempCaretStart);
            lookingForStart = false;
          }
          else
          {
            tempCaretStart -= this._textElement.childNodes[i].textContent.length;
          }
        }
        if(!lookingForStart)
          break;
    }
    var tempCaretEnd = tempCaretStart + caret.end - caret.start;
    for(; i < this._textElement.childNodes.length; i++)
    {
        if(this._textElement.childNodes[i].nodeType == Node.ELEMENT_NODE)
        {
          var currentElement : HTMLElement = <HTMLElement>this._textElement.childNodes[i];

          if(currentElement.tagName == "BR")
          {
            tempCaretEnd = tempCaretEnd==0?0:tempCaretEnd - 1;
          }
          else
          {
              if(currentElement.textContent.length >= tempCaretEnd)
              {
                  // found the right element
                  for(let ni = 0; ni < currentElement.childNodes.length; ni++)
                  {
                      if(currentElement.childNodes[ni].nodeType == Node.TEXT_NODE)
                      {
                        range.setEnd(currentElement.childNodes[ni], tempCaretEnd); 
                        var sel = window.getSelection();
                        sel.removeAllRanges();
                        sel.addRange(range);
                        return;
                      }
                  }
              }
              else
              {
                  tempCaretEnd -= currentElement.textContent.length;
              }
          }
        }
        else if(this._textElement.childNodes[i].nodeType == Node.TEXT_NODE)
        {
          if(this._textElement.childNodes[i].textContent.length >= tempCaretEnd)
          {
            var sel = window.getSelection();
            range.setEnd(this._textElement.childNodes[i], tempCaretEnd);  
            sel.removeAllRanges();
            sel.addRange(range);
            return;
          }
          else
          {
            tempCaretEnd -= this._textElement.childNodes[i].textContent.length;
          }
        }
    }

    // console.log("creating text node inside div");
    var newEmptyNode = document.createTextNode("");
    this._textElement.appendChild(newEmptyNode);

    var sel = window.getSelection();
    range.setStart(newEmptyNode, 0);
    range.collapse(true);    
    sel.removeAllRanges();
    sel.addRange(range);   
    // console.log("newTN-sel", sel);
  }

  public userInput() : void {
    // console.log("userinput");

    if (!this.editable)
    {
      console.log("Editing not allowed - ignoring character!");
      return;
    }

    if (!this.editableNow)
    {
      console.log("Too fast typing - ignoring character!");
      return;
    }
    // console.log("Not editable now");
    this.editableNow = false;

    var idx = this._caretPosition;
    this.text = this.textFromRender();  // to invoke re-render
    // console.log("tfr: ", this.text);
    setTimeout(() => {
      // console.log(`CI: ${idx.toString()}`);
      // console.log("Edeitable now");
      this.editableNow = true;
      this._caretPosition = idx;
      // console.log("CP: ", idx, this._caretPosition);
    });
  }

  private render() : void {
    if(this._viewInitialized)
    {
      var carretIdx = this._caretPosition;
      // console.log("render", this._text);
      var textToRender : string = this._text;
      // textToRender = textToRender.replace(/ /g, "&nbsp");
      var originalLength = textToRender.length;

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

      // var hItems : HighlightItem[] = this.highlighter?this.highlighter.generateHighlightedItems(textToRender):[];
      // var highlighterClassName : string = this.highlighter?this.highlighter.highlighterClassName():"";

      var lastAddedWasBR = false;
      var currentTextOffset : number = 0;
      var currentHItem : HighlightItem = null;
      var partStartIdx : number = 0;
      var partEndIdx : number = 0;
      var lineEndIdx : number = 0;
      var tabIdx : number = 0;
      var idxType : HighlightIndexType = HighlightIndexType.none;
      var idx : number = 0;
      var hiIdx : number = 0;
      while(textToRender.length > 0)
      {
        currentTextOffset = originalLength - textToRender.length;
        // console.log("Text is before: ", textToRender);
        lineEndIdx = textToRender.indexOf(this._internalEnterRepresentation);
        tabIdx = textToRender.indexOf(this._internalTabRepresentation);
        currentHItem = currentHItem? currentHItem : this._highlightingItems[hiIdx++];
        if(!currentHItem)
        {
          if(lineEndIdx >= 0)
          {
            idxType = HighlightIndexType.lineEnd;
            idx = lineEndIdx;
          }
          else
          {
            idxType = HighlightIndexType.none;
            idx = textToRender.length - 1;
          }
        }
        else
        {
          partStartIdx = currentHItem.start - currentTextOffset;
          partEndIdx = currentHItem.end - currentTextOffset;

          // the part has not started yet
          if(partStartIdx > 0)
          {
            if(lineEndIdx >= 0 && lineEndIdx <= partStartIdx)
            {
              if(tabIdx >=0 && tabIdx < lineEndIdx)
              {
                idxType = HighlightIndexType.tab;
                idx = tabIdx;
              }
              else
              {
                idxType = HighlightIndexType.lineEnd;
                idx = lineEndIdx;
              }
            }
            else
            {
              if(tabIdx >=0 && tabIdx <= partStartIdx)
              {
                idxType = HighlightIndexType.tab;
                idx = tabIdx;
              }
              else
              {
                idxType = HighlightIndexType.partStart;
                idx = partStartIdx;
              }
            }
          }
          else  // inside some highlighted part
          {
            if(lineEndIdx >= 0 && lineEndIdx <= partEndIdx)
            {
              if(tabIdx >=0 && tabIdx < lineEndIdx)
              {
                idxType = HighlightIndexType.tab;
                idx = tabIdx;
              }
              else
              {
                // before lineEnd create the span with proper class
                idxType = HighlightIndexType.lineEnd;
                idx = lineEndIdx;
              }
            }
            else
            {
              if(tabIdx >=0 && tabIdx <= partEndIdx)
              {
                idxType = HighlightIndexType.tab;
                idx = tabIdx;
              }
              else
              {
                idxType = HighlightIndexType.partEnd;
                idx = partEndIdx;
              }
            }
          }
        } 

        switch(idxType)
        {
          case HighlightIndexType.none:
            this.requirementParts.push(new RequirementPart(textToRender));
            textToRender = ""; // end of the while, this is last element
            lastAddedWasBR = false;
            break;
          case HighlightIndexType.lineEnd:
            if(idx == 0)
            {
              // two be able to target cursor between empty lines just add empty span in between
              if(lastAddedWasBR)
              {
                // console.log("SPAN - Adding empty SPAN between BR");
                this.requirementParts.push(new RequirementPart(""));
              }

              // console.log("BR - Now there is new line here");
              this.requirementParts.push(new RequirementPart("","","",true));
              textToRender = textToRender.replace(this._internalEnterRepresentation,"");

              if(partEndIdx == lineEndIdx)
              {
                currentHItem = null;
              }

              lastAddedWasBR = true;
            }
            else {
              (currentHItem && partStartIdx <= 0)?this.requirementParts.push(new RequirementPart(textToRender.substr(0, idx), currentHItem.styleClass + ((currentHItem.comment.length > 0)?" " + ColorableTextAreaComponent.__partHoverClass:""), currentHItem.comment)):this.requirementParts.push(new RequirementPart(textToRender.substr(0, idx)));
              textToRender = textToRender.slice(idx); 
              lastAddedWasBR = false;
            }
            break;
          case HighlightIndexType.tab:
            if(idx == 0)
            {
              (currentHItem && partStartIdx <= 0)?this.requirementParts.push(new RequirementPart(this._internalTabSubstitute, this._internalTabClass + " " + currentHItem.styleClass + ((currentHItem.comment.length > 0)?" " + ColorableTextAreaComponent.__partHoverClass:""), currentHItem.comment)):this.requirementParts.push(new RequirementPart(this._internalTabSubstitute, this._internalTabClass));
              textToRender = textToRender.slice(this._internalTabRepresentation.length); 

              if(partEndIdx == tabIdx)
              {
                currentHItem = null;
              }

            }
            else {
              (currentHItem && partStartIdx <= 0)?this.requirementParts.push(new RequirementPart(textToRender.substr(0, idx), currentHItem.styleClass + ((currentHItem.comment.length > 0)?" " + ColorableTextAreaComponent.__partHoverClass:""), currentHItem.comment)):this.requirementParts.push(new RequirementPart(textToRender.substr(0, idx)));
              textToRender = textToRender.slice(idx); 
            }
            lastAddedWasBR = false;
            break;
          case HighlightIndexType.partStart:
              this.requirementParts.push(new RequirementPart(textToRender.substr(0, idx))); // idx was tested before that is always at least 1
              textToRender = textToRender.slice(idx); 
              lastAddedWasBR = false;
            break;
          case HighlightIndexType.partEnd:
              this.requirementParts.push(new RequirementPart(textToRender.substr(0, idx + 1), currentHItem.styleClass + ((currentHItem.comment.length > 0)?" " + ColorableTextAreaComponent.__partHoverClass:""), currentHItem.comment));
              textToRender = textToRender.slice(idx + 1); 
              currentHItem = null;
              lastAddedWasBR = false;
            break; 
        }
      }

      // make sure the editable does not end with br element as the last line would be not targetable for seting the cursor
      if(lastAddedWasBR)
      {
        // console.log("BR - Fake last BR");
        this.requirementParts.push(new RequirementPart("","","",true));
      }

      this.cdr.detectChanges();

      var textElementCoordinates = this._textElement.getBoundingClientRect();

      for(let i = 0; i < this._textElement.children.length; i++)
      {
        if(this._textElement.children[i].classList.contains(this._internalTabClass))
        {
          var elementCoordinates = this._textElement.children[i].getBoundingClientRect();

          var relativeLeft = elementCoordinates.left - textElementCoordinates.left;

          var spaceFromLeft = relativeLeft + elementCoordinates.width;

          var newSpaceFromLeft = (Math.floor(spaceFromLeft / this._tabIndentation) + 1) * this._tabIndentation;

          var spaceDiff = Math.round(newSpaceFromLeft - spaceFromLeft);

          (<HTMLElement>this._textElement.children[i]).style.paddingLeft = `${spaceDiff}px`;
        }
      }

      this._caretPosition = carretIdx;
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
        else if((<HTMLElement>this._textElement.childNodes[i]).classList.contains(this._internalTabClass))
        {
          // console.log("TFR: SPAN: ", this._textElement.childNodes[i].textContent);
          text += this._internalTabRepresentation;
        }
        else
        {
          text += this._textElement.childNodes[i].textContent;
        }
      }
      else if(this._textElement.childNodes[i].nodeType == Node.TEXT_NODE)
      {
        // console.log("TFR: TEXT in ROOT: ", this._textElement.childNodes[i].textContent);
        text += this._textElement.childNodes[i].textContent;//.replace(/\u00A0/g, " ");
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

  public partHover(idx : number)
  {
    if(this.requirementParts[idx].comment.length > 0)
    {
      this._hintElement.innerHTML = this.requirementParts[idx].comment;
      this._hintElement.style.opacity = "1.0";
      this._hintElement.style.left = "0px";

      var anchorElementPosition : DOMRect = this._textElement.children[idx].getBoundingClientRect();
      if(anchorElementPosition.top > (window.innerHeight - anchorElementPosition.bottom))
      {
        this._hintElement.style.top = null;
        this._hintElement.style.bottom = (window.innerHeight - anchorElementPosition.top).toString() + "px";
        // this._hintElement.style.top = anchorElementPosition.top.toString() + "px";

      }
      else
      { 
        this._hintElement.style.bottom = null;
        this._hintElement.style.top = anchorElementPosition.bottom.toString() + "px";
      }

      // this._hintElement.style.top = anchorElementPosition.left.toString() + "px";

      var widthReserve : number = window.innerWidth - anchorElementPosition.left - this._hintElement.getBoundingClientRect().width;
      this._hintElement.style.left = ((widthReserve >= 0)?anchorElementPosition.left.toString():(anchorElementPosition.left + widthReserve).toString()) + "px";
    }
  }

  public partHoverEnd(idx :number)
  {
    this._hintElement.innerHTML = "";
    this._hintElement.style.visibility = "0.0";
  }



}
