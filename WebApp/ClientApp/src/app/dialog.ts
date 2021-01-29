import { DialogOption } from './dialog-option';
import { DialogType } from './dialog-type';

export class Dialog {
    dialogType : DialogType;
    title : string = "";
    text : string = "";
    options : Array<DialogOption> = [];
    defaultOptionIdx : number = null;
    cancelCallback : () => void = null; // if not specified, the cancel option on top of dialog whould not be present

    constructor(text : string, title : string, dialogType : DialogType, options : Array<DialogOption> = [new DialogOption("OK")], defaultOptionIdx : number = null, cancelCallack : () => void = null)
    {
        this.title = title;
        this.text = text;
        this.dialogType = dialogType;
        this.options = options;
        this.defaultOptionIdx = defaultOptionIdx;
        this.cancelCallback = cancelCallack;
    }
}