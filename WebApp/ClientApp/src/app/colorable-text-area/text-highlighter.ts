import { HighlightItem } from "./highlight-item"

export class TextHighlighter {

   private _internalEnterRepresentation : string = "\n";

   protected highlighterClassName : string = "";

   public generateHighlightedItems(text : string) : HighlightItem[]
   {
      return [];
   }
   
   public mergeLists(to : HighlightItem[], from : HighlightItem[]) : void
   {
      let lastIndex : number = 0;
      for(let item of from)
      {
         lastIndex = this.insertingNewHighlightItem(item, to, lastIndex) + 1;
      }
   }

   // returning the last index into which it inserts the newItem last part
   protected insertingNewHighlightItem(newItem : HighlightItem, items : HighlightItem[], indexToStartFrom : number = 0) : number
   {
      let niscWithHCN, niscWithoutHCN : string; //  = newItem.styleClass with/without the preceding highlighter class name

      if(newItem.styleClass.indexOf(this.highlighterClassName + " ") == 0)
      {
         niscWithHCN = newItem.styleClass;
         niscWithoutHCN = newItem.styleClass.substr(this.highlighterClassName.length + 1);
      }
      else
      {
         niscWithHCN = this.highlighterClassName + " " + newItem.styleClass;
         niscWithoutHCN = newItem.styleClass;
      }

       for(let i : number = indexToStartFrom; i < items.length; i++)
       {
           let itemFromArray : HighlightItem = items[i];
           if(newItem.start <= itemFromArray.end)
           {
               if(newItem.end < itemFromArray.start)
               {
                   // it should be before this item - not overlaping it
                   items.splice(i, 0, newItem.styleClass.indexOf(this.highlighterClassName + " ") == 0?newItem: new HighlightItem(newItem.start, newItem.end, niscWithHCN, newItem.comment));
                   return i;
               }
               else
               {
                   // it overlaps somehow
                   if(newItem.start < itemFromArray.start)
                   {
                       // some portion of newItem is before the item from array, but not whole
                       if(newItem.end > itemFromArray.end)
                       {
                           // whole item from array is inside the new item -> let's make 1 new item part + 1 combined part + 1 that will continue to search (recursively)
                           items.splice(i, 0, new HighlightItem(newItem.start, itemFromArray.start - 1, niscWithHCN, newItem.comment));
                           itemFromArray.styleClass += " " + niscWithoutHCN;
                           itemFromArray.comment += this._internalEnterRepresentation + newItem.comment;
                           return this.insertingNewHighlightItem(new HighlightItem(itemFromArray.end + 1, newItem.end, niscWithHCN, newItem.comment), items, i + 2);
                       }
                       else
                       {
                           // newItem starts before the item in array and ends somewhere inside it -> let's make 1 newItem part + 1 combined + 1 original (if does not end at same position as the newitem)
                           items.splice(i, 0, new HighlightItem(newItem.start, itemFromArray.start - 1, niscWithHCN, newItem.comment));
                           if(itemFromArray.end == newItem.end)
                           {
                               // newItems and itemFromArray end at same spot
                               itemFromArray.styleClass += " " + niscWithoutHCN;
                               itemFromArray.comment += this._internalEnterRepresentation + newItem.comment;
                           }
                           else
                           {
                               items.splice(i + 1, 0, new HighlightItem(itemFromArray.start, newItem.end, itemFromArray.styleClass + " " + niscWithoutHCN, itemFromArray.comment + this._internalEnterRepresentation + newItem.comment));
                               itemFromArray.start = newItem.end + 1;
                           }
                           return i + 1;
                       }
                   }
                   else
                   {
                       // newItem ends somewhere inside the itemFromArray
                       if(newItem.end <= itemFromArray.end)
                       {
                           // newItem ends somewhere inside itemFromArray
                           if(newItem.start == itemFromArray.start)
                           {
                               // the items starts at same place
                               if(newItem.end == itemFromArray.end)
                               {
                                   // the items are identical in position  -> update the styleClass
                                   itemFromArray.styleClass += " " + niscWithoutHCN;
                                   itemFromArray.comment += this._internalEnterRepresentation + newItem.comment;
                               }
                               else
                               {
                                   // items start at same position but the newItem ends before the itemFromArray -> create 1 combined class and shorten the class already in array
                                   items.splice(i, 0, new HighlightItem(newItem.start, newItem.end, itemFromArray.styleClass + " " + niscWithoutHCN, itemFromArray.comment + this._internalEnterRepresentation + newItem.comment));
                                   itemFromArray.start = newItem.end + 1;
                                   return;
                               }
                               return i;
                           }
                           else
                           {
                               // the newItem starts after the itemFromArray
                               items.splice(i, 0, new HighlightItem(itemFromArray.start, newItem.start - 1, itemFromArray.styleClass, itemFromArray.comment));
                               if(newItem.end == itemFromArray.end)
                               {
                                   // from start of the newItem items are identical in length  -> update the styleClass
                                   itemFromArray.styleClass += " " + niscWithoutHCN;
                                   itemFromArray.comment += this._internalEnterRepresentation + newItem.comment;
                               }
                               else
                               {
                                   // newItem is inside the itemFromArray -> create 1 more combined class and shorten the class already in array
                                   items.splice(i + 1, 0, new HighlightItem(newItem.start, newItem.end, itemFromArray.styleClass + " " + niscWithoutHCN, itemFromArray.comment + this._internalEnterRepresentation + newItem.comment));
                                   itemFromArray.start = newItem.end + 1;
                               }
                               return i + 1;
                           }
                       }
                       else
                       {
                           // newItem ends after the itemFromArray
                           if(newItem.start == itemFromArray.start)
                           {
                               // starts at same spot but newItem spreads further -> change the itemFromArray to combined class and recursively insert the rest of newItem
                               itemFromArray.styleClass += " " + niscWithoutHCN;
                               itemFromArray.comment += this._internalEnterRepresentation + newItem.comment;
                               return this.insertingNewHighlightItem(new HighlightItem(itemFromArray.end + 1, newItem.end, niscWithHCN, newItem.comment), items, i + 1);
                           }
                           else
                           {
                               // newItem starts after itemFromArray and spreads beyond the itemFromArray => insert new part with
                               items.splice(i, 0, new HighlightItem(itemFromArray.start, newItem.start - 1, itemFromArray.styleClass, itemFromArray.comment));
                               itemFromArray.start = newItem.start;
                               itemFromArray.styleClass += " " + niscWithoutHCN;
                               itemFromArray.comment += this._internalEnterRepresentation + newItem.comment;
                               return this.insertingNewHighlightItem(new HighlightItem(itemFromArray.end + 1, newItem.end, niscWithHCN, newItem.comment), items, i + 2);
                           }
                       }
                   }
               }
           }
       }
       // newItem starts after everything in items has ended, so let's push it there
       items.push(newItem.styleClass.indexOf(this.highlighterClassName + " ") == 0?newItem: new HighlightItem(newItem.start, newItem.end, niscWithHCN, newItem.comment));
   }
}
