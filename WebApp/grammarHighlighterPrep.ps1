if($args[0])
{
    $dir = $args[0] + '\';
}
else
{
    $dir = '.\';
}

function GenerateGrammarHighlighting($inFile, $outFile) {
    $groups = @{}
    
    foreach($line in Get-Content $inFile) {
        if($line -match $regex){
            $terminalCandidates = $Matches['groupLine'].Split("'");
            if($terminalCandidates.length -gt 2)
            {
                if(-not ($groups.ContainsKey($Matches['groupName'])))
                {
                    $groups[$Matches['groupName']] = @();
                }
    
                for($i=1; $i -lt $terminalCandidates.length; $i += 2)
                {
                    if($i -lt $terminalCandidates.length - 1)  # is still enclosed by apostrophes
                    {
                        $terminal = $terminalCandidates[$i].Trim();
                        if($groups[$Matches['groupName']] -notcontains $terminal)
                        {
                            $groups[$Matches['groupName']] += $terminal;
                        }
                    }
                }
            }
        }
    }
    
    $resultFile = '[';
    $firstGroup = $TRUE;
    
    foreach($g in $groups.Keys)
    {
        if($firstGroup)
        {
            $firstGroup = $FALSE;
        }
        else
        {
            $resultFile += ',';
        }
    
        $resultFile += '{"group": "' + $g + '","terms": ['
    
        $firstTerm = $TRUE;
        foreach($t in $groups[$g])
        {
            if($firstTerm)
            {
                $firstTerm = $FALSE;
            }
            else
            {
                $resultFile += ',';
            }
    
            $resultFile += '"' + $t + '"';
        }
    
        $resultFile += ']}';
    }
    
    $resultFile += ']';
    
    Set-Content -Path $outFile -Value $resultFile
}


$regex = '(?<groupLine>.*)//\s?highlight as (?<groupName>\w+)'
$outFile = $dir + 'ClientApp\src\app\colorable-text-area\ears-highlighter\grammar.json'
$inFile = $dir + '..\InterLayerLib\HonPBRL.g4'

GenerateGrammarHighlighting -inFile $inFile -outFile $outFile

$outFile = $dir + 'ClientApp\src\app\colorable-text-area\clips-highlighter\grammar.json'
$inFile = $dir + '..\InterLayerLib\CLIPS.g4'

GenerateGrammarHighlighting -inFile $inFile -outFile $outFile