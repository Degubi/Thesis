import requests
from shutil import copyfileobj

magyarlanc_url = 'https://rgai.inf.u-szeged.hu/sites/rgai.inf.u-szeged.hu/files/magyarlanc-3.0.jar'

print(f'Downloading magyarlanc from {magyarlanc_url}')

with open('magyarlanc.jar', 'wb') as magyarlanc_output, requests.get(magyarlanc_url, stream = True) as magyarlanc_input:
    copyfileobj(magyarlanc_input.raw, magyarlanc_output)

print('Done!')