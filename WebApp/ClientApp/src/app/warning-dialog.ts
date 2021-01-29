import { DialogType } from './dialog-type';
import { DialogOption } from './dialog-option';
import { Dialog } from './dialog';

export class WarningDialog extends Dialog {
    constructor(text : string, callbackOnAcknowladge : () => void = null, title : string = "Warning")
    {
        super(text, title, DialogType.warning, [new DialogOption("ACKNOWLADGE", callbackOnAcknowladge)]);
    }
}
