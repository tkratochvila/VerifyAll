export class EditMemory {

    private items : {
        undo : () => void,
        redo : () => void
    }[] = [];

    private idx : number = 0;

    constructor()
    {
    }

    public clearForwardHistory() : void {
        this.items = this.items.slice(0, this.idx);
    }

    public clearAllHistory() : void {
        this.items = [];
        this.idx = 0;
    }

    public new(undo : () => void, redo : () => void) : void {
        // console.log("Before new", this.idx, this.items.length);
        this.clearForwardHistory();
        this.items.push({undo: undo, redo: redo});
        this.idx++;

        // console.log("After new", this.idx, this.items.length);
    }

    public undo() : boolean {
        // console.log("Before undo", this.idx, this.items.length);
        if(this.idx > 0)
        {
            this.items[this.idx - 1].undo();
            this.idx--;

            // console.log("After undo", this.idx, this.items.length);
            return true;
        }
        else
        {
            return false;
        }
    }

    public redo() : boolean {
        console.log("Before redo", this.idx, this.items.length);
        if(this.idx < this.items.length)
        {
            this.items[this.idx].redo();
            this.idx++;

            console.log("After redo", this.idx, this.items.length);
            return true;
        }
        else
        {
            return false;
        }
    }




    // private undos : EditLog[] = [];
    // private redos : EditLog[] = [];

    // private deleteReqDelegate : (idx : number) => void;
    // private createReqDelegate : (idx : number, text : string) => void;
    // private changeTextDelegate : (reqIdx : number, startIdx : number, endIdx : number, newText : string) => void;

    // constructor(deleteRequirementDelegate : (idx : number) => void, createRequirementDelegate : (idx : number, text : string) => void, changeTextDelegate : (reqIdx : number, startIdx : number, endIdx : number, newText : string) => void)
    // {
    //     this.deleteReqDelegate = deleteRequirementDelegate;
    //     this.createReqDelegate = createRequirementDelegate;
    //     this.changeTextDelegate = changeTextDelegate;
    // }

    // clearRedos() : void {
    //     this.redos = [];
    // }

    // clearUndos() : void {
    //     this.undos = [];
    // }

    // clearAll() : void {
    //     this.clearRedos();
    //     this.clearUndos();      
    // }


}
