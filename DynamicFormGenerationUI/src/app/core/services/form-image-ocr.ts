import { Injectable, OnDestroy } from '@angular/core';
import { createWorker, Worker } from 'tesseract.js';
import { OcrLine, OcrResult } from '../models/form-image.model';

/**
 * Reads the text of a form image in the browser with Tesseract.js (OCR).
 *
 * The OCR files (worker, engine and English data) are served by this app from /tesseract/
 * (copied there by angular.json), so OCR works without internet and the image never leaves the browser.
 */
@Injectable({
  providedIn: 'root',
})
export class FormImageOcrService implements OnDestroy {
  private static readonly OCR_LANGUAGE = 'eng';
  /** OCR engine mode 1 = LSTM neural network only (best accuracy, matches the English data we ship). */
  private static readonly OCR_ENGINE_MODE = 1;

  private iobjWorkerPromise: Promise<Worker> | null = null;

  /** Reads the text lines of the image, with the position and confidence of every word. */
  async readImage(aObjFile: File): Promise<OcrResult> {
    const lobjWorker = await this.getWorker();
    const lnumImageWidth = await this.readImageWidth(aObjFile);
    const lobjResult = await lobjWorker.recognize(aObjFile, {}, { blocks: true, text: true });
    const larrLines: OcrLine[] = [];

    for (const lobjBlock of lobjResult.data.blocks ?? []) {
      for (const lobjParagraph of lobjBlock.paragraphs) {
        for (const lobjLine of lobjParagraph.lines) {
          const larrWords = lobjLine.words
            .filter(aObjWord => aObjWord.text.trim().length > 0)
            .map(aObjWord => ({
              text: aObjWord.text.trim(),
              x0: aObjWord.bbox.x0,
              x1: aObjWord.bbox.x1,
              confidence: aObjWord.confidence
            }));

          if (larrWords.length > 0) {
            larrLines.push({
              top: lobjLine.bbox.y0,
              height: Math.max(1, lobjLine.bbox.y1 - lobjLine.bbox.y0),
              words: larrWords
            });
          }
        }
      }
    }

    larrLines.sort((aObjFirst, aObjSecond) => aObjFirst.top - aObjSecond.top);

    return {
      text: (lobjResult.data.text ?? '').trim(),
      confidence: Math.round(lobjResult.data.confidence ?? 0),
      imageWidth: lnumImageWidth,
      lines: larrLines
    };
  }

  /** Stops the OCR worker and frees its memory. A new worker is created on the next read. */
  async terminate(): Promise<void> {
    const lobjWorkerPromise = this.iobjWorkerPromise;
    this.iobjWorkerPromise = null;

    if (lobjWorkerPromise) {
      try {
        await (await lobjWorkerPromise).terminate();
      } catch {
        // The worker failed to start, so there is nothing to stop.
      }
    }
  }

  ngOnDestroy(): void {
    void this.terminate();
  }

  /** Creates the OCR worker once and reuses it; the first read loads about 10 MB of OCR data. */
  private getWorker(): Promise<Worker> {
    if (!this.iobjWorkerPromise) {
      const lstrBaseUrl = new URL('tesseract/', document.baseURI).href;

      this.iobjWorkerPromise = createWorker(FormImageOcrService.OCR_LANGUAGE, FormImageOcrService.OCR_ENGINE_MODE, {
        workerPath: `${lstrBaseUrl}worker.min.js`,
        corePath: `${lstrBaseUrl}core`,
        langPath: `${lstrBaseUrl}lang`,
        workerBlobURL: false,
        gzip: true
      });

      // If the worker fails to start, allow a new try on the next read.
      this.iobjWorkerPromise.catch(() => { this.iobjWorkerPromise = null; });
    }

    return this.iobjWorkerPromise;
  }

  /** Returns the width of the image in pixels, used to calculate column spans. */
  private async readImageWidth(aObjFile: File): Promise<number> {
    try {
      const lobjBitmap = await createImageBitmap(aObjFile);
      const lnumWidth = lobjBitmap.width;
      lobjBitmap.close();
      return lnumWidth;
    } catch {
      return 0;
    }
  }
}
