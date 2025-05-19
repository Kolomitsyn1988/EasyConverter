document.addEventListener('DOMContentLoaded', function () {

    const dropZone = document.getElementById('dropZone');
    const fileInput = document.getElementById('fileInput');
    const fileName = document.getElementById('fileName');
    const conversionSection = document.getElementById('conversionSection');
    const convertBtn = document.getElementById('convertBtn');
    const progressBar = document.getElementById('progressBar');
    const progressSection = document.querySelector('.progress');
    const statusMessage = document.getElementById('statusMessage');
    const formatSelect = document.getElementById('formatSelect');

    let selectedFile = null;

    // Drag and drop handlers
    dropZone.addEventListener('dragover', (e) => {
        e.preventDefault();
        dropZone.classList.add('dragover');
    });

    dropZone.addEventListener('dragleave', () => {
        dropZone.classList.remove('dragover');
    });

    dropZone.addEventListener('drop', (e) => {
        e.preventDefault();
        dropZone.classList.remove('dragover');
        const files = e.dataTransfer.files;
        handleFileSelection(files[0]);
    });

    fileInput.addEventListener('change', (e) => {
        handleFileSelection(e.target.files[0]);
    });

    function handleFileSelection(file) {
        if (!file) return;

        const validTypes = ['video/mp4', 'video/webm', 'video/avi', 'video/quicktime', 'video/x-matroska'];
        if (!validTypes.includes(file.type)) {
            showStatus('Please select a valid video file (mp4, webm, avi, mov, or mkv).', 'danger');
            return;
        }

        selectedFile = file;
        fileName.textContent = file.name;
        conversionSection.style.display = 'block';
        statusMessage.style.display = 'none';
    }

    convertBtn.addEventListener('click', async () => {
        if (!selectedFile) {
            showStatus('Please select a video file first.', 'danger');
            return;
        }

        const formData = new FormData();
        formData.append('file', selectedFile);

        try {
            // Upload the file
            convertBtn.disabled = true;
            progressSection.style.display = 'block';
            showStatus('Uploading video...', 'info');

            const uploadResponse = await fetch('/api/video/upload', {
                method: 'POST',
                body: formData
            });

            if (!uploadResponse.ok) {
                throw new Error('Failed to upload video');
            }

            const uploadResult = await uploadResponse.json();
            if (!uploadResult) {
                throw new Error('Failed to start convert video');
            }

            showStatus('Converting video...', 'info');
            const convertResponse = await fetch('/api/video/convert', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    fileName: uploadResult.fileName,
                    outputFormat: formatSelect.value
                })
            });

            if (!convertResponse.ok) {
                throw new Error('Failed to convert video');
            }

            const convertResult = await convertResponse.json();
            if (!convertResult.originalFile) {
                throw new Error('Failed video url');
            }

            // Create download link
            showStatus('Conversion completed! <a href="' + convertResult.originalFile + '" class="alert-link">Download converted video</a>', 'success');
        } catch (error) {
            showStatus('Error: ' + error.message, 'danger');
        } finally {
            convertBtn.disabled = false;
            progressBar.style.width = '0%';
            progressBar.textContent = '0%';
        }
    });

    function showStatus(message, type) {
        statusMessage.style.display = 'block';
        statusMessage.className = 'alert alert-' + type;
        statusMessage.innerHTML = message;
    }
});
