package magyarlancanalyzer;

import java.nio.file.*;
import java.util.*;
import magyarlancanalyzer.model.*;

public final class Main {

    public static void main(String[] args) throws Exception {
        var sentences = analyzeTextFile("extract.txt");

    }

    public static MagyarlancSentence[] analyzeTextFile(String inputFilePath) throws Exception {
        System.out.println("Analyzing text from: " + inputFilePath);

        var magyarlancOutputFile = "analyse.txt";
        var magyarlancOutputPath = Path.of(magyarlancOutputFile);

        Runtime.getRuntime().exec("java -jar magyarlanc.jar -mode morphparse -input " + inputFilePath + " -output " + magyarlancOutputFile).waitFor();

        var magyarlancOutput = Files.readString(magyarlancOutputPath);

        Files.delete(magyarlancOutputPath);

        return Arrays.stream(magyarlancOutput.split("\n\n"))
                     .map(MagyarlancSentence::new)
                     .toArray(MagyarlancSentence[]::new);
    }
}