package szakdoga;

import java.awt.geom.*;
import java.io.*;
import java.nio.file.*;
import java.util.*;
import java.util.regex.*;
import org.apache.pdfbox.pdmodel.*;
import org.apache.pdfbox.text.*;
import szakdoga.model.*;

public final class Main {
    private static final Pattern jibberishCharacterReplacer = Pattern.compile("▶|•| ");

    public static void main(String[] args) throws Exception {
        var sentences = analyzePDFText("pdfs/9/szgy.pdf");

    }

    public static MagyarlancSentence[] analyzePDFText(String pdfPath) throws InterruptedException, IOException {
        var extractionOutputFile = "tempOut.txt";
        var extractionOutputPath = Path.of(extractionOutputFile);

        System.out.println("Extracting text from: " + pdfPath);

        try(var inputPDFDocument = PDDocument.load(Path.of(pdfPath).toFile());
            var outputTextFile = Files.newBufferedWriter(extractionOutputPath)) {

            var textStripper = new PDFTextStripperByArea();
            var firstPageBBox = inputPDFDocument.getPage(1).getBBox();

            textStripper.addRegion("SkipMargin", new Rectangle2D.Double(40, 40, firstPageBBox.getWidth() - 80, firstPageBBox.getHeight() - 80));

            var pages = inputPDFDocument.getPages();
            for(var i = 11; i < 16; ++i) {
                textStripper.extractRegions(pages.get(i));

                textStripper.getTextForRegion("SkipMargin")
                            .lines()
                            .forEach(k -> {
                                try {
                                    outputTextFile.write(k);
                                    outputTextFile.write('\n');
                                } catch (IOException e) {
                                    e.printStackTrace();
                                }
                            });
            }
        }

        System.out.println("Analyzing text from: " + pdfPath);

        var magyarlancOutputFile = "analyse.txt";
        var magyarlancOutputPath = Path.of(magyarlancOutputFile);

        Runtime.getRuntime().exec("java -jar magyarlanc.jar -mode morphparse -input " + extractionOutputFile + " -output " + magyarlancOutputFile).waitFor();

        System.out.println("Done with: " + pdfPath);

        var magyarlancOutput = Files.readString(magyarlancOutputPath);

        Files.delete(extractionOutputPath);
        Files.delete(magyarlancOutputPath);

        return Arrays.stream(magyarlancOutput.split("\n\n"))
                     .map(MagyarlancSentence::new)
                     .toArray(MagyarlancSentence[]::new);
    }
}