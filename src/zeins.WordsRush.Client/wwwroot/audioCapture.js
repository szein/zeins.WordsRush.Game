let mediaStream;
let mediaRecorder;
let audioChunks = [];

export async function startRecording() {
    mediaStream = await navigator.mediaDevices.getUserMedia({ audio: true });
    const preferredTypes = ["audio/webm;codecs=opus", "audio/webm", "audio/ogg"];
    const mimeType = preferredTypes.find(type => MediaRecorder.isTypeSupported(type));

    mediaRecorder = mimeType
        ? new MediaRecorder(mediaStream, { mimeType })
        : new MediaRecorder(mediaStream);
    audioChunks = [];
    mediaRecorder.ondataavailable = event => {
        if (event.data.size > 0) {
            audioChunks.push(event.data);
        }
    };
    mediaRecorder.start();
}

export async function stopRecording() {
    if (!mediaRecorder || mediaRecorder.state === "inactive") {
        return new Uint8Array();
    }

    const stopped = new Promise(resolve => {
        mediaRecorder.onstop = async () => {
            const blob = new Blob(audioChunks, { type: mediaRecorder.mimeType });
            const wav = await convertToWav(blob);
            resolve(new Uint8Array(wav));
        };
    });
    mediaRecorder.stop();
    mediaStream.getTracks().forEach(track => track.stop());
    return stopped;
}

export function disposeRecording() {
    if (mediaRecorder && mediaRecorder.state !== "inactive") {
        mediaRecorder.stop();
    }
    if (mediaStream) {
        mediaStream.getTracks().forEach(track => track.stop());
    }
}

async function convertToWav(blob) {
    const context = new AudioContext();
    try {
        const sourceBuffer = await blob.arrayBuffer();
        const audioBuffer = await context.decodeAudioData(sourceBuffer);
        const sampleRate = 16000;
        const length = Math.ceil(audioBuffer.duration * sampleRate);
        const offlineContext = new OfflineAudioContext(1, length, sampleRate);
        const source = offlineContext.createBufferSource();
        source.buffer = audioBuffer;
        source.connect(offlineContext.destination);
        source.start();

        const resampledBuffer = await offlineContext.startRendering();
        const output = resampledBuffer.getChannelData(0);
        const wav = new ArrayBuffer(44 + length * 2);
        const view = new DataView(wav);
        writeString(view, 0, "RIFF");
        view.setUint32(4, 36 + length * 2, true);
        writeString(view, 8, "WAVE");
        writeString(view, 12, "fmt ");
        view.setUint32(16, 16, true);
        view.setUint16(20, 1, true);
        view.setUint16(22, 1, true);
        view.setUint32(24, sampleRate, true);
        view.setUint32(28, sampleRate * 2, true);
        view.setUint16(32, 2, true);
        view.setUint16(34, 16, true);
        writeString(view, 36, "data");
        view.setUint32(40, length * 2, true);

        let offset = 44;
        for (let i = 0; i < length; i++) {
            const sample = Math.max(-1, Math.min(1, output[i]));
            view.setInt16(offset, sample < 0 ? sample * 0x8000 : sample * 0x7fff, true);
            offset += 2;
        }

        return wav;
    } finally {
        await context.close();
    }
}

function writeString(view, offset, value) {
    for (let i = 0; i < value.length; i++) {
        view.setUint8(offset + i, value.charCodeAt(i));
    }
}
