
export class CaretHelper {
    private static rangeIsSelected(element) : boolean {
        var doc = element.ownerDocument || element.document;
        var win = doc.defaultView || doc.parentWindow;
        var range = win.getSelection().getRangeAt(0);
        return !(range.startContainer == range.endContainer && range.startOffset == range.endOffset);
    }

    public static afterLineBreak(element) : boolean {
        var doc = element.ownerDocument || element.document;
        var win = doc.defaultView || doc.parentWindow;
        console.log(element, win);
        var range = win.getSelection().getRangeAt(0);

        if(CaretHelper.rangeIsSelected(element))
        {
            return false;
        }

        // is the curet before any content on selected element?
        if(range.endOffset == 0)
        {
            // find if <BR> element is before this element
            var selectedElement = range.endContainer;
            if(selectedElement.nodeType == Node.TEXT_NODE)
            {
                var previousElement = selectedElement.parentNode.previousSibling;
                if(previousElement && previousElement.tagName == "BR")
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                //??? is this means what exactly? is it some node like <br> without textnode?
                return false;
            }
        }
        else
        {
            return false;
        }
    }

    // public static getCaretCharacterOffsetWithin(element) : number {
    //     var caretOffset = 0;
    //     var doc = element.ownerDocument || element.document;
    //     var win = doc.defaultView || doc.parentWindow;
    //     var sel;
    //     // if (typeof win.getSelection != "undefined") {
    //         sel = win.getSelection();
    //         if (sel.rangeCount > 0) {
    //             var range = win.getSelection().getRangeAt(0);
    //             var preCaretRange = range.cloneRange();
    //             preCaretRange.selectNodeContents(element);
    //             console.log("range", range.endContainer, range.endOffset);
    //             preCaretRange.setEnd(range.endContainer, range.endOffset);
    //             caretOffset = preCaretRange.toString().length;
    //         }
    //     // } else if ( (sel = doc.selection) && sel.type != "Control") {
    //     //     var textRange = sel.createRange();
    //     //     var preCaretTextRange = doc.body.createTextRange();
    //     //     preCaretTextRange.moveToElementText(element);
    //     //     preCaretTextRange.setEndPoint("EndToEnd", textRange);
    //     //     caretOffset = preCaretTextRange.text.length;
    //     // }
    //     return caretOffset;
    // }

    public static getCaretCharacterOffsetWithin(element) : number {
        console.log("getCaretCharacterOffsetWithin");
        var caretOffset : number = 0;
        var doc = element.ownerDocument || element.document;
        var win = doc.defaultView || doc.parentWindow;
        var sel;
        sel = win.getSelection();
        if (sel.rangeCount > 0) {
            var range : Range = <Range>(win.getSelection().getRangeAt(0));
            var selectedElement : HTMLElement = range.endContainer.parentElement;
            console.log("Selected element in getPosition: ", selectedElement);
            for(var i = 0; i < element.children.length; i++)
            {
                if(selectedElement == element.children[i])
                {
                    caretOffset += range.endOffset;
                    console.log(`Found selected element as ${i}th children. Adding ${range.endOffset} to caretOffset which is: ${caretOffset}`);
                    return caretOffset;
                }
                else
                {
                    if(element.children[i].tagName == "BR")
                    {
                        caretOffset++;
                        console.log(`Found BR element. Adding 1 to caretOffset which is now: ${caretOffset}`);
                    }
                    else
                    {
                        caretOffset += element.children[i].textContent.length;
                        console.log(`Found span element. Adding ${element.children[i].textContent.length} to caretOffset which is now: ${caretOffset}`);
                    }
                }
            }
        }
        return 0;
    }

    public static setCaretPosition(el : HTMLElement, position : number) {
        var tempIdx = position;
        
        for(let ii = 0; ii < el.childNodes.length; ii++)
        {
            console.log("child", ii, el.childNodes[ii].nodeType, el.childNodes[ii]);
        }

        console.log(`Finding position ${position} of cursor`);
        var foundElementWithCursor = false;
        var finalNode : ChildNode = undefined;
        var i : number = 0;

        for(; i < el.children.length; i++)
        {
            var currentElement : HTMLElement = <HTMLElement>el.children[i];

            if(currentElement.tagName == "BR")
            {
                tempIdx--;
            }
            else
            {
                if(currentElement.textContent.length >= tempIdx)
                {
                    // found the right element
                    for(let ni = 0; ni < currentElement.childNodes.length; ni++)
                    {
                        if(currentElement.childNodes[ni].nodeType == Node.TEXT_NODE)
                        {
                            var range = document.createRange();
                            var sel = window.getSelection();
                            // range.setStart(el.childNodes[2], 5)
                            console.log(`Set start from ${el.children.length} children count as ${i} and the offset within left out were ${tempIdx}`);
                            range.setStart(currentElement.childNodes[ni], tempIdx);
                            range.collapse(true);
                            
                            sel.removeAllRanges();
                            sel.addRange(range);
                            return;
                        }
                    }
                }
                else
                {
                    tempIdx -= currentElement.textContent.length;
                }
            }
        }
    }

    // public static getCaretPosition() {
    //     if (window.getSelection && window.getSelection().getRangeAt) {
    //       var range = window.getSelection().getRangeAt(0);
    //       var selectedObj = window.getSelection();
    //       var rangeCount = 0;
    //       var childNodes = selectedObj.anchorNode.parentNode.childNodes;
    //       for (var i = 0; i < childNodes.length; i++) {
    //         if (childNodes[i] == selectedObj.anchorNode) {
    //           break;
    //         }
    //         if (childNodes[i].nodeType == Node.TEXT_NODE) {
    //           rangeCount += childNodes[i].textContent.length;
    //         }
    //       }
    //       return range.startOffset + rangeCount;
    //     }
    //     return -1;
    // }

    // public static setCaretPosition(el : HTMLElement, position : number, onNewLine : boolean = true) {
        
        
    //     var tempIdx = position;
    //     var i : number = 0;
    //     for(let ii = 0; ii < el.childNodes.length; ii++)
    //     {
    //         console.log("child", ii, el.childNodes[ii].nodeType, el.childNodes[ii]);
    //     }

    //     console.log(`Finding position ${position} of cursor`);
    //     var foundElementWithCursor = false;
    //     var finalNode : ChildNode = undefined;
    //     for(; i < el.children.length; i++)
    //     {

    //         var child = el.children[i];
    //         for(let ni = 0; ni < el.children[i].childNodes.length; ni++)
    //         {
    //             if(el.children[i].childNodes[ni].nodeType == Node.TEXT_NODE)
    //             {
    //                 var contentLength = el.children[i].childNodes[ni].textContent.length;
    //                 console.log(`Found text node in ${i}h children with content length ${contentLength}`);
    //                 if(tempIdx > contentLength)
    //                 {
    //                     console.log(`TempIdx is before substraction ${tempIdx}`);
    //                     tempIdx -= contentLength;
    //                     console.log(`TempIdx is after substraction ${tempIdx}`);
    //                 }
    //                 else if(onNewLine && i < (el.children.length - 1))
    //                 {
    //                     console.log(`We should switch to next line: ${tempIdx}=${contentLength}`);
    //                     ++i;
    //                     // if the next element is BR, move it to nother one, so the cursor is on the right position
    //                     if(el.children[i].tagName == "BR")
    //                     {
    //                         ++i;
    //                     }
    //                     // finalNode = el.children[++i];
    //                     tempIdx = 0;    // to be at start of the next element
    //                     foundElementWithCursor = true;
    //                     for(let iii = 0; iii < el.children[i].childNodes.length; iii++)
    //                     {
    //                         if(el.children[i].childNodes[iii].nodeType == Node.TEXT_NODE)
    //                         {
    //                             console.log("Text node in BR element");
    //                             finalNode = el.children[i].childNodes[iii];
    //                             break;
    //                         }
    //                     }
    //                     break;
    //                 }
    //                 else
    //                 {
    //                     console.log(`We are already at the desired children: ${tempIdx}>=${contentLength}`);
    //                     finalNode = el.children[i].childNodes[ni];
    //                     foundElementWithCursor = true;
    //                     break;
    //                 }
    //             }
    //         }

    //         if(foundElementWithCursor)
    //             break;
    //     }

    //     var range = document.createRange();
    //     var sel = window.getSelection();
    //     // range.setStart(el.childNodes[2], 5)
    //     console.log(`Set start from ${el.children.length} children count as ${i} and the offset within left out were ${tempIdx}`);
    //     console.log(`Final node is: ${finalNode}`);
    //     range.setStart(finalNode, tempIdx);
    //     range.collapse(true);
        
    //     sel.removeAllRanges();
    //     sel.addRange(range)
    // }
}
