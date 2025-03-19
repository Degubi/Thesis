from os import mkdir, remove
import requests
from shutil import copyfileobj, rmtree
from pathlib import Path
from subprocess import call

def download_file_to(url, output_file):
    print(f"Downloading file '{output_file}' from '{url}'")

    with open(output_file, 'wb') as output, requests.get(url, stream = True) as input:
        copyfileobj(input.raw, output)


if(Path('TextExtractor/pdfs').exists()):
    rmtree('TextExtractor/pdfs')

if(Path('TextExtractor/tessdata').exists()):
    rmtree('TextExtractor/tessdata')

mkdir('TextExtractor/pdfs')
mkdir('TextExtractor/tessdata')

download_file_to('https://rgai.inf.u-szeged.hu/sites/rgai.inf.u-szeged.hu/files/magyarlanc-3.0.jar', 'magyarlanc.jar')
download_file_to('https://raw.githubusercontent.com/tesseract-ocr/tessdata/main/hun.traineddata', 'TextExtractor/tessdata/hun.traineddata')
download_file_to('https://raw.githubusercontent.com/tesseract-ocr/tessdata/main/osd.traineddata', 'TextExtractor/tessdata/osd.traineddata')

download_file_to('https://www.tankonyvkatalogus.hu/storage/pdf/OH-MIR09SZ__teljes.pdf', 'TextExtractor/pdfs/9_szgy.pdf')
download_file_to('https://www.tankonyvkatalogus.hu/storage/pdf/OH-MIR09TA__teljes.pdf', 'TextExtractor/pdfs/9_tk.pdf')

download_file_to('https://www.tankonyvkatalogus.hu/storage/pdf/FI-501021002_1__teljes.pdf', 'TextExtractor/pdfs/10_szgy.pdf')
download_file_to('https://www.tankonyvkatalogus.hu/storage/pdf/FI-501021001_1__teljes.pdf', 'TextExtractor/pdfs/10_tk.pdf')

download_file_to('https://www.tankonyvkatalogus.hu/storage/pdf/FI-501021102_1__teljes.pdf', 'TextExtractor/pdfs/11_szgy.pdf')
download_file_to('https://www.tankonyvkatalogus.hu/storage/pdf/FI-501021101_1__teljes.pdf', 'TextExtractor/pdfs/11_tk.pdf')

download_file_to('https://www.tankonyvkatalogus.hu/storage/pdf/FI-501021202_1__teljes.pdf', 'TextExtractor/pdfs/12_szgy.pdf')
download_file_to('https://www.tankonyvkatalogus.hu/storage/pdf/FI-501021201_1__teljes.pdf', 'TextExtractor/pdfs/12_tk.pdf')

call('mvn install:install-file -Dfile=magyarlanc.jar -DgeneratePom=true -Dversion=3.0 -Dpackaging=jar -DgroupId=rgai.inf.u.szeged.hu -DartifactId=magyarlanc -Dsources=magyarlanc_src.zip')
remove('magyarlanc.jar')
call('pip install matplotlib')

print('Done!')