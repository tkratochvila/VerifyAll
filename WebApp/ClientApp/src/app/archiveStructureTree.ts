import { ArchiveFileItem } from "./ArchiveFileItem"

export class ArchiveStructureTree {
    public fileName : string = "";
    public activeDirectoryPath : string = "";
    public activeDirectoryFiles : Array<ArchiveFileItem> = [];
    public rootDirectory : boolean = true;
    public unpackedView : boolean = false;

    private subFiles : ArchiveFileItem[] = [];
    private pathIdxs : number[] = [];

    constructor()
    {
        
    }

    public reloadFromArchiveTreeStructure(aTree : any)
    {
        this.fileName = aTree.fileName;
        
        for(let sf of aTree.subFiles)
        {
            this.subFiles.push(this.newFileItem(sf));
        }

        this.regenerateActiveDirectoryParams();
    }

    public resetArchiveTreeStructure()
    {
        this.fileName = "";
        this.activeDirectoryPath = "";
        this.activeDirectoryFiles = [];
        this.rootDirectory = true;
        this.subFiles = [];
        this.pathIdxs = [];
    }

    private newFileItem(newFileItem : any) : ArchiveFileItem
    {
        var afi : ArchiveFileItem = new ArchiveFileItem();

        afi.fileName = newFileItem.fileName;
        
        if(newFileItem.subFiles && Array.isArray(newFileItem.subFiles))
        {
            afi.subFiles = [];

            for(let sf of newFileItem.subFiles)
            {
                afi.subFiles.push(this.newFileItem(sf));
            }
        }
        else
        {
            afi.subFiles = null;
        }

        return afi;
    }

    private regenerateActiveDirectoryParams() 
    {
        if(this.pathIdxs.length == 0)
        {
            this.rootDirectory = true;
            this.activeDirectoryPath = "";
            this.activeDirectoryFiles = this.subFiles;
        }
        else
        {
            var sfTemp = this.subFiles[this.pathIdxs[0]];
            var path : string = "\\" + sfTemp.fileName + "\\";

            for(let i = 1; i < this.pathIdxs.length; i++)
            {
                sfTemp = sfTemp.subFiles[this.pathIdxs[i]];
                path += sfTemp.fileName + "\\";
            }

            this.rootDirectory = false;
            this.activeDirectoryPath = path;
            this.activeDirectoryFiles = sfTemp.subFiles;
        }
    }

    public goDirectoryUp()
    {
        if(this.pathIdxs.length > 0)
        {
            this.pathIdxs.pop();
            this.regenerateActiveDirectoryParams();
        }
    }

    public goDirectorySelect(idx : number)
    {
        if(this.activeDirectoryFiles && this.activeDirectoryFiles.length > idx && this.activeDirectoryFiles[idx].subFiles)
        {
            this.pathIdxs.push(idx);
            this.regenerateActiveDirectoryParams();
        }
    }

    public goDirectoryRoot()
    {
        this.pathIdxs = [];
        this.regenerateActiveDirectoryParams();
    }
}
