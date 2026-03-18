using System;
using TMPro;
using Unity.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using ZXing;

public class ARQrScanner : MonoBehaviour
{
    [Header("AR")]
    [SerializeField] private ARCameraManager arCameraManager;

    [Header("UI")]
    [SerializeField] private Button scanButton;
    [SerializeField] private Button stopButton;
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private GameObject qrAnimationPanel;

    [Header("Scan Settings")]
    [SerializeField] private float scanInterval = 0.25f;
    [SerializeField] private bool autoStopOnDetect = true;

    [SerializeField] ARDynamicTracker arDynamicTracker; 

    public Action<string> OnQrDetected;
    public Action<string> OnArtworkIdDetected;

    private bool isScanning;
    private float nextScanTime;
    private IBarcodeReader barcodeReader;
    private Texture2D cameraTexture;
    private Color32[] pixelBuffer;

    private void Awake()
    {
        barcodeReader = new BarcodeReader
        {
            AutoRotate = false,
            Options = new ZXing.Common.DecodingOptions
            {
                PossibleFormats = new System.Collections.Generic.List<BarcodeFormat>
                {
                    BarcodeFormat.QR_CODE
                },
                TryHarder = true
            }
        };

        if (scanButton != null)
            scanButton.onClick.AddListener(StartScanning);

        if (stopButton != null)
            stopButton.onClick.AddListener(StopScanning);

        stopButton.gameObject.SetActive(false);
    }
    

    private void OnDestroy()
    {
        if (scanButton != null)
            scanButton.onClick.RemoveListener(StartScanning);

        if (stopButton != null)
            stopButton.onClick.RemoveListener(StopScanning);

        if (cameraTexture != null)
        {
            Destroy(cameraTexture);
            cameraTexture = null;
        }
    }
    private void Start()
    {
        //OnQrCodeFound("https://api.scripictural.tecshield.net/api/artworks/public/69b8ebbb9f9befc7a7cd0156");
        //OnQrCodeFound("https://d1j44teybnnehj.cloudfront.net/?id=69b8ebbb9f9befc7a7cd0156");
        //Invoke(nameof(InvokeQrCode), 15f);
    }

    private void InvokeQrCode()
    {
        //OnQrCodeFound("https://api.scripictural.tecshield.net/api/artworks/public/69b8edb09f9befc7a7cd0179");
    }
    public void StartScanning()
    {
        if (arCameraManager == null)
        {
            Debug.LogError("ARCameraManager is not assigned.");
            return;
        }

        isScanning = true;
        nextScanTime = 0f;

        qrAnimationPanel.SetActive(true);
        stopButton.gameObject.SetActive(true);
        scanButton.gameObject.SetActive(false);
        if (resultText != null)
            resultText.text = "Scanning QR...";
    }

    public void StopScanning()
    {
        qrAnimationPanel.SetActive(false);
        stopButton.gameObject.SetActive(false);
        scanButton.gameObject.SetActive(true);
        isScanning = false;
    }

    private void Update()
    {
        if (!isScanning)
            return;

        if (Time.time < nextScanTime)
            return;

        nextScanTime = Time.time + scanInterval;
        TryScanQr();
    }

    private void TryScanQr()
    {
        if (!arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage cpuImage))
            return;

        if(!isScanning)
            return;

        try
        {
            var conversionParams = new XRCpuImage.ConversionParams
            {
                inputRect = new RectInt(0, 0, cpuImage.width, cpuImage.height),
                outputDimensions = new Vector2Int(cpuImage.width, cpuImage.height),
                outputFormat = TextureFormat.RGBA32,
                transformation = XRCpuImage.Transformation.MirrorY
            };

            int size = cpuImage.GetConvertedDataSize(conversionParams);

            using NativeArray<byte> buffer = new NativeArray<byte>(size, Allocator.Temp);
            cpuImage.Convert(conversionParams, buffer);

            if (cameraTexture == null || cameraTexture.width != cpuImage.width || cameraTexture.height != cpuImage.height)
            {
                if (cameraTexture != null)
                    Destroy(cameraTexture);

                cameraTexture = new Texture2D(cpuImage.width, cpuImage.height, TextureFormat.RGBA32, false);
                pixelBuffer = new Color32[cpuImage.width * cpuImage.height];
            }

            cameraTexture.LoadRawTextureData(buffer);
            cameraTexture.Apply(false);

            pixelBuffer = cameraTexture.GetPixels32();

            var result = barcodeReader.Decode(pixelBuffer, cameraTexture.width, cameraTexture.height);

            if (result != null && !string.IsNullOrWhiteSpace(result.Text))
            {
                OnQrCodeFound(result.Text);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("QR scan error: " + ex.Message);
        }
        finally
        {
            cpuImage.Dispose();
        }
    }

    private void OnQrCodeFound(string decodedText)
    {
        Debug.Log("QR detected: " + decodedText);
        qrAnimationPanel.SetActive(false);

        if (resultText != null)
            resultText.text = decodedText;

        OnQrDetected?.Invoke(decodedText);

        string artworkId = ExtractArtworkIdFromUrl(decodedText);
        if (!string.IsNullOrEmpty(artworkId))
        {
            Debug.Log("Artwork ID extracted: " + artworkId);
            OnArtworkIdDetected?.Invoke(artworkId);
            arDynamicTracker.OnArtworkIdReceived(artworkId);
        }
        else
        {
            Debug.LogWarning("Could not extract artwork ID from QR text: " + decodedText);
        }

        if (autoStopOnDetect)
            StopScanning();
    }
    //public static string ExtractArtworkIdFromUrl(string value)
    //{
    //    if (string.IsNullOrWhiteSpace(value))
    //        return null;

    //    if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri uri))
    //        return null;

    //    string path = uri.AbsolutePath.Trim('/');
    //    if (string.IsNullOrWhiteSpace(path))
    //        return null;

    //    string[] parts = path.Split('/');
    //    if (parts.Length == 0)
    //        return null;

    //    return parts[parts.Length - 1];
    //}

    public static string ExtractArtworkIdFromUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri uri))
            return null;

        // Get query string (?id=xxxx)
        string query = uri.Query;
        if (string.IsNullOrEmpty(query))
            return null;

        // Remove '?'
        query = query.TrimStart('?');

        // Parse key-value pairs
        string[] pairs = query.Split('&');
        foreach (var pair in pairs)
        {
            string[] kv = pair.Split('=');
            if (kv.Length == 2 && kv[0] == "id")
            {
                print(kv[1]);
                return kv[1];

            }
        }

        return null;
    }
    public bool IsValidUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out Uri uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    public void OpenIfUrl(string value)
    {
        if (IsValidUrl(value))
            Application.OpenURL(value);
    }
}