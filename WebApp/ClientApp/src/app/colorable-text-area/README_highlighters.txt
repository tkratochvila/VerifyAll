To add new highlighter:
1) create new folder in the name of the highlighter
2) create new class by "ng g class colorable-text-area/<highlighter-folder>/<HighlighterClassName>"
3) add "implements TextHighlighter" to the class and implements those methods accordingly
    - "highlighterClassName" method should return the name of the highlighter (the name is tehn used in the styles as well)
    - "generateHighlightedItems" method should return array of highlighted segments (segments cannot overlap)
4) create style file for the highlighter in the newly created folder (less or css) - name of the file is good to be the same as the highlighter class
5) add the name of the style file into "styleUrls" of "@Component" in "colorable-text-area-component.ts"
6) adds or make sure it is added: "import { <HighlighterClassName> } from './colorable-text-area/<highlighter-folder>/<HighlighterClassName>';" to "app.component.ts"
7) add "<HighlighterClassName>" option to switch in "chooseHighlighter" method in "app.component.ts"