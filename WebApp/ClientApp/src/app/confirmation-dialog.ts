import { DialogType } from './dialog-type';
import { DialogOption } from './dialog-option';
import { Dialog } from './dialog';

export class ConfirmationDialog extends Dialog {
    constructor(text : string, callbackOnConfirmation : () => void, callbackOnCancel : () => void = () => {}, title : string = "Please confirm", confirmationPreffered : boolean = true, confirmationButtonText : string = "YES")
    {
        super(text, title, DialogType.confirmation, confirmationPreffered?[new DialogOption(confirmationButtonText, callbackOnConfirmation), new DialogOption("CANCEL", callbackOnCancel)]:[new DialogOption("CANCEL", callbackOnCancel), new DialogOption(confirmationButtonText, callbackOnConfirmation)], 0, callbackOnCancel);
    }
}
