from os import mkdir
import requests
from shutil import copyfileobj, rmtree
from pathlib import Path

def download_file_to(url, output_file):
    print(f"Downloading file '{output_file}' from '{url}'")

    with open(output_file, 'wb') as output, requests.get(url, stream = True) as input:
        copyfileobj(input.raw, output)


if(Path('pdfs').exists()):
    rmtree('pdfs')

mkdir('pdfs')
mkdir('pdfs/9')
mkdir('pdfs/10')
mkdir('pdfs/11')
mkdir('pdfs/12')

download_file_to('https://rgai.inf.u-szeged.hu/sites/rgai.inf.u-szeged.hu/files/magyarlanc-3.0.jar', 'magyarlanc.jar')

download_file_to('https://www.tankonyvkatalogus.hu/pdf/OH-MIR09SZ__teljes.pdf', 'pdfs/9/szgy.pdf')
download_file_to('https://www.tankonyvkatalogus.hu/pdf/OH-MIR09TA__teljes.pdf', 'pdfs/9/tk.pdf')

download_file_to('https://www.tankonyvkatalogus.hu/pdf/FI-501021002_1__teljes.pdf', 'pdfs/10/szgy.pdf')
download_file_to('https://www.tankonyvkatalogus.hu/pdf/FI-501021001_1__teljes.pdf', 'pdfs/10/tk.pdf')



print('Done!')