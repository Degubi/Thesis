package magyarlancanalyzer.model;

import java.util.*;
import java.util.stream.*;

public final class MagyarlancSentence {

    public final String fullSentence;
    public final MagyarlancWord[] words;

    public MagyarlancSentence(String sentenceText) {
        var analysisLinesPerWord = sentenceText.lines()
                                               .map(k -> k.split("\t"))
                                               .toArray(String[][]::new);

        fullSentence = Arrays.stream(analysisLinesPerWord)
                             .map(k -> k[0])
                             .collect(Collectors.joining(" "));

        words = Arrays.stream(analysisLinesPerWord)
                      .map(MagyarlancWord::new)
                      .toArray(MagyarlancWord[]::new);
    }
}