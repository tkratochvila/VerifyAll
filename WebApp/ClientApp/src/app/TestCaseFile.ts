import {ArchiveStructureTree} from "./archiveStructureTree"
import { FileInfo } from "./file-info"

export class TestCaseFile {
    public fileName : string;
    public info : string;
    public archiveStructure : ArchiveStructureTree = null;
    public isLoaded : boolean;

    constructor(file : FileInfo)
    {
        this.fileName = file.fileName;
        this.info = file.info?file.info:"";
        this.isLoaded = file.fileName.toUpperCase().endsWith(".ZIP")?false:true;
    }
}
