import { DialogType } from './dialog-type';
import { DialogOption } from './dialog-option';
import { Dialog } from './dialog';

export class MultichoiceDialog extends Dialog {
    constructor(text : string, title : string, options : DialogOption[], primaryOptionIndex : number = null)
    {
        super(text, title, DialogType.multichoice, options, primaryOptionIndex);
    }
}
