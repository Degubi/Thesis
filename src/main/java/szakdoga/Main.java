package szakdoga;

import java.awt.geom.*;
import java.io.*;
import java.nio.file.*;
import org.apache.pdfbox.pdmodel.*;
import org.apache.pdfbox.text.*;

public final class Main {

    public static void main(String[] args) throws IOException {
        try(var inputPDFDocument = PDDocument.load(Path.of("OH-MIR09SZ__teljes.pdf").toFile());
            var outputTextFile = Files.newBufferedWriter(Path.of("output.txt"))) {
            var textStripper = new PDFTextStripperByArea();

            var pageWidth = inputPDFDocument.getPage(1).getBBox().getWidth();
            var pageHeight = inputPDFDocument.getPage(1).getBBox().getHeight();

            textStripper.setSortByPosition(true);
            textStripper.addRegion("SkipMargin", new Rectangle2D.Double(40, 40, pageWidth - 80, pageHeight - 80));

            for(var page : inputPDFDocument.getPages()) {
                textStripper.extractRegions(page);

                outputTextFile.write(textStripper.getTextForRegion("SkipMargin"));
            }
        }
    }
}