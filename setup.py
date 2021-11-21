from os import mkdir
import requests
from shutil import copyfileobj, rmtree
from pathlib import Path

def download_file_to(url, output_file):
    print(f"Downloading file '{output_file}' from '{url}'")

    with open(output_file, 'wb') as output, requests.get(url, stream = True) as input:
        copyfileobj(input.raw, output)


if(Path('TextExtractor/pdfs').exists()):
    rmtree('TextExtractor/pdfs')

# if(Path('TextExtractor/tessdata').exists()):
#     rmtree('TextExtractor/tessdata')

mkdir('TextExtractor/pdfs')
mkdir('TextExtractor/pdfs/9')
mkdir('TextExtractor/pdfs/10')
mkdir('TextExtractor/pdfs/11')
mkdir('TextExtractor/pdfs/12')
# mkdir('TextExtractor/tessdata')

#download_file_to('https://rgai.inf.u-szeged.hu/sites/rgai.inf.u-szeged.hu/files/magyarlanc-3.0.jar', 'MagyarlancAnalyzer/magyarlanc.jar')
#download_file_to('https://raw.githubusercontent.com/tesseract-ocr/tessdata/main/hun.traineddata', 'TextExtractor/tessdata/hun.traineddata')
#download_file_to('https://raw.githubusercontent.com/tesseract-ocr/tessdata/main/osd.traineddata', 'TextExtractor/tessdata/osd.traineddata')

download_file_to('https://www.tankonyvkatalogus.hu/pdf/OH-MIR09SZ__teljes.pdf', 'TextExtractor/pdfs/9/szgy.pdf')
download_file_to('https://www.tankonyvkatalogus.hu/pdf/OH-MIR09TA__teljes.pdf', 'TextExtractor/pdfs/9/tk.pdf')

download_file_to('https://www.tankonyvkatalogus.hu/pdf/FI-501021002_1__teljes.pdf', 'TextExtractor/pdfs/10/szgy.pdf')
download_file_to('https://www.tankonyvkatalogus.hu/pdf/FI-501021001_1__teljes.pdf', 'TextExtractor/pdfs/10/tk.pdf')

download_file_to('https://www.tankonyvkatalogus.hu/pdf/FI-501021102_1__teljes.pdf', 'TextExtractor/pdfs/11/szgy.pdf')
download_file_to('https://www.tankonyvkatalogus.hu/pdf/FI-501021101_1__teljes.pdf', 'TextExtractor/pdfs/11/tk.pdf')

download_file_to('https://www.tankonyvkatalogus.hu/pdf/FI-501021202_1__teljes.pdf', 'TextExtractor/pdfs/12/szgy.pdf')
download_file_to('https://www.tankonyvkatalogus.hu/pdf/FI-501021201_1__teljes.pdf', 'TextExtractor/pdfs/12/tk.pdf')

print('Done!')