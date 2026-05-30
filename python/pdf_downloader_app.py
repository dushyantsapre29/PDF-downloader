import sys
import os
from PyQt6.QtWidgets import QApplication, QLabel, QWidget, QPushButton, QVBoxLayout, QFileDialog, QLineEdit, QMessageBox, QListWidget, QAbstractItemView, QListWidgetItem
from PyQt6.QtCore import Qt

import requests
from bs4 import BeautifulSoup
import webbrowser as wb
from urllib.parse import urljoin
from urllib.request import urlretrieve

class PDFDownloader(QWidget):
    """
    A class which covers an app which downloads pdfs from a given link
    """
    def __init__(self):
        super().__init__()

        self.setWindowTitle("PDF Downloader")

        # Create layout TODO what does this do. why is this not self.
        layout = QVBoxLayout()

        # Create label for that link text box
        self.label = QLabel("Paste your link below:")

        # Create input box
        self.link_input_box = QLineEdit()
        self.link_input_box.setPlaceholderText("Paste link here..")

        # Scrape button
        self.scrape_button = QPushButton("Scrape PDFs")
        self.scrape_button.clicked.connect(self.scrape_pdf_urls)

        # Download button
        self.download_button = QPushButton("Download Selected PDF")
        self.download_button.clicked.connect(self.download_pdfs)


        # List of PDFs
        self.list_pdfs = QListWidget()
        self.list_pdfs.setSelectionMode(QAbstractItemView.SelectionMode.ExtendedSelection) # making it so one can select multiple files like in file explorer

        # adding widgets to layout
        layout.addWidget(self.label)
        layout.addWidget(self.link_input_box)
        layout.addWidget(self.scrape_button)
        layout.addWidget(self.list_pdfs)
        layout.addWidget(self.download_button)

        # set layout for the window
        self.setLayout(layout)


    def scrape_pdf_urls(self):
        """
        This function scrapes the pdf urls on the webpage given by the user's link 
        """
        url = self.link_input_box.text().strip()
        if not url:
            QMessageBox.warning(self, "Error", "Please enter a valid URL")
            return 
        
        try:
            # Add the URL of the webpage containing the links to the PDFs

            response = requests.get(url)
            soup = BeautifulSoup(response.text, 'html.parser')


            # Extracts all links and their text from the webpage
            links = [(link.get('href'), link.get_text(strip=True)) for link in soup.find_all("a")]

            # Extract only PDF links and make them absolute URLs
            pdf_links = [(link,text) for (link,text) in links if link.endswith('.pdf')]
            pdf_links = [(urljoin(url, link),text) for (link,text) in pdf_links]

            self.list_pdfs.clear()

            for link, text in pdf_links: # loop to add link and text to the list, but make text visible and store link in a hidden way
                item = QListWidgetItem(text) # user will see text
                item.setData(Qt.ItemDataRole.UserRole, link) # apparently UserRole is a hidden pocket to store custom data, there are other roles for other stuff like icons, tooltips, etc
                self.list_pdfs.addItem(item)

                # self.list_pdfs.addItems(pdf_links)


            if not pdf_links:
                QMessageBox.information(self, "No PDFs", "No PDF files found on this page")


        except Exception as e:
            QMessageBox.critical(self, "Error", str(e))

    def download_pdfs(self):
        """
        this function downloads the pdfs
        """
        selected_items = self.list_pdfs.selectedItems()
        if not selected_items:
            QMessageBox.warning(self, "No Selection", "Please select at least one PDF")
            return

        # choosing folder to select PDFs
        folder = QFileDialog.getExistingDirectory(self, "Select Download folder")
        if not folder: # TODO should we be adding an error warning here?
            return
        
        for item in selected_items:
            link = item.data(Qt.ItemDataRole.UserRole) # this retrieves the link we stored before
            text = item.text()

            try:
                response = requests.get(link, allow_redirects=True)
                
                download_path = os.path.join(folder, text)

                with open(download_path,"wb") as file:
                    print(f'Downloading {text} from {link}...')
                    file.write(response.content)

            except Exception as e:
                QMessageBox.critical(self, "Error", f'failed to download {text} from {link}')

        QMessageBox.information(self, "Done", "All selected PDFs have been downloaded")


# creating an application object here
app = QApplication([])

# creating a window object here
window = PDFDownloader()
window.setWindowTitle("PDF Downloader")

window.show()
app.exec()

