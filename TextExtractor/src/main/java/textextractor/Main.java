package textextractor;

import java.io.*;
import java.nio.file.*;
import java.util.*;
import java.util.regex.*;
import java.util.stream.*;
import javax.imageio.*;
import net.sourceforge.tess4j.*;
import org.apache.pdfbox.pdmodel.*;
import org.apache.pdfbox.rendering.*;

public final class Main {
    private static final boolean IMAGE_DEBUG = false;

    public static void main(String[] args) throws Exception {
        var params = IntStream.iterate(0, k -> k < args.length, k -> k + 2)
                              .boxed()
                              .collect(Collectors.toMap(k -> args[k], k -> args[k + 1]));

        if(params.isEmpty()) {
            System.out.println("Options: -input {pdfPath} -pageRange {n-m}");
            return;
        }

        var inputPDFPath = Path.of(params.get("-input"));
        var extractionOutputFileName = "extract";
        var optionalPageRange = params.get("-pageRange");
        var splitPageRange = optionalPageRange != null ? optionalPageRange.split("-") : null;

        try(var inputPDFDocument = PDDocument.load(inputPDFPath.toFile())) {
            var imageRenderer = new PDFRenderer(inputPDFDocument);
            var tesseract = ThreadLocal.withInitial(Main::createTesseract);
            var lineEndingRegex = Pattern.compile("-\n|\n");

            var startingPage = splitPageRange == null ? 1 : Integer.parseInt(splitPageRange[0]) - 1;
            var endingPage = splitPageRange == null ? inputPDFDocument.getNumberOfPages() - 1 : Integer.parseInt(splitPageRange[1]) - 1;

            System.out.println("Extracting text from: " + inputPDFPath + ", page range: " + startingPage + "-" + endingPage);

            var startTime = System.currentTimeMillis();

            var rawExtractedText = IntStream.rangeClosed(startingPage, endingPage)
                                            .parallel()
                                            .mapToObj(i -> extractTextFromPage(imageRenderer, tesseract, i))
                                            .sorted(Comparator.comparingInt(ExtractResult::page))
                                            .map(ExtractResult::content)
                                            .collect(Collectors.joining());

            var perParagraphExtract = Arrays.stream(rawExtractedText.split("\n\n"))
                                            .map(k -> lineEndingRegex.matcher(k).replaceAll(""))
                                            .collect(Collectors.joining("\n"));

            var endTime = System.currentTimeMillis();

            Files.writeString(Path.of(extractionOutputFileName + "_raw.txt"), rawExtractedText);
            Files.writeString(Path.of(extractionOutputFileName + "_paragraph.txt"), perParagraphExtract);

            System.out.println("Done in " + (endTime - startTime) + "ms!");
        }
    }


    private static ExtractResult extractTextFromPage(PDFRenderer imageRenderer, ThreadLocal<Tesseract> tesseract, int page) {
        try {
            var image = imageRenderer.renderImageWithDPI(page, 300);
            var croppedImage = image.getSubimage(100, 100, image.getWidth() - 200, image.getHeight() - 200);

            if(IMAGE_DEBUG) {
                ImageIO.write(croppedImage, "png", new File("image-extract-" + page + ".png"));
            }

            return new ExtractResult(tesseract.get().doOCR(croppedImage), page);
        } catch (IOException | TesseractException e) {
            e.printStackTrace();
            return new ExtractResult("", 0);
        }
    }

    private static Tesseract createTesseract() {
        var tesseract = new Tesseract();
        tesseract.setDatapath(Path.of("tessdata").toAbsolutePath().toString());
        tesseract.setTessVariable("user_defined_dpi", "300");
        tesseract.setPageSegMode(1);
        tesseract.setLanguage("hun");
        return tesseract;
    }

    record ExtractResult(String content, int page) {}
}