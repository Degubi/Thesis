package analyzers;

import static java.nio.file.StandardOpenOption.*;

import edu.stanford.nlp.pipeline.*;
import edu.stanford.nlp.util.*;
import java.io.*;
import java.nio.charset.*;
import java.nio.file.*;
import java.util.*;

public final class CoreNLPRunner {

    public static void main(String[] args) throws IOException {
        var outputDirPath = Path.of("../outputs/analyze/corenlp");
        if(!Files.exists(outputDirPath)) {
            Files.createDirectory(outputDirPath);
        }

        var pipeline = new StanfordCoreNLP(PropertiesUtils.asProperties(
                "annotators", "tokenize, ssplit, pos, lemma, ner",
                "cdc_tokenize.model", "edu/stanford/nlp/models/cdc-tokenize/hu-tokenizer.ser.gz",
                "pos.model", "edu/stanford/nlp/models/pos-tagger/hungarian.tagger",
                "parse.model", "edu/stanford/nlp/models/srparser/hungarianSR.beam.ser.gz",
                "ner.model", "edu/stanford/nlp/models/ner/hungarian.crf.ser.gz",
                "ner.applyFineGrained", "false",
                "ner.applyNumericClassifiers", "false",
                "ner.useSUTime", "false")
        );

        var filesToAnalyze = Files.list(Path.of("../outputs/text/paraphrised")).toArray(Path[]::new);
        var totalStartTime = System.currentTimeMillis();

        System.out.println("Analyzing " + filesToAnalyze.length + " files\n");

        Arrays.stream(filesToAnalyze).parallel()
              .forEach(k -> analyzeWithCoreNLP(k, outputDirPath, pipeline));

        var totalEndTime = System.currentTimeMillis();
        System.out.println("\nAll done in " + (totalEndTime - totalStartTime) + "ms!");
    }

    private static void analyzeWithCoreNLP(Path inputFile, Path outputDirPath, StanfordCoreNLP pipeline) {
        System.out.println("Analyzing file: " + inputFile);

        try(var output = Files.newBufferedWriter(Path.of(outputDirPath.toString() + '/' + inputFile.getFileName()), StandardCharsets.UTF_8, TRUNCATE_EXISTING, CREATE)) {
            var document = new CoreDocument(Files.readString(inputFile));
            pipeline.annotate(document);

            for(var sentence : document.sentences()) {
                var posTags = sentence.posTags();

                for(var token : sentence.tokens()) {
                    output.write(token.word() + '\t' + token.lemma() + '\t' + posTags.get(token.index() - 1) + '\n');
                }

                output.append('\n');
            }
        } catch (IOException e) {
            e.printStackTrace();
        }

        System.out.println("Done with file: " + inputFile);
    }
}