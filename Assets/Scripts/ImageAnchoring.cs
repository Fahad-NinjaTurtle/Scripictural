using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ImageAnchoring : MonoBehaviour
{
    //[SerializeField] private ARTrackedImageManager imageManager;
    //[SerializeField] private ARAnchorManager anchorManager;
    //[SerializeField] private GameObject contentPrefab;

    //// These can be private; Unity calls them via reflection
    //private void OnEnable()
    //{
    //    if (imageManager != null)
    //    {
    //        // Use AddListener instead of +=
    //        imageManager.trackablesChanged.AddListener(OnTrackablesChanged);
    //    }
    //}

    //private void OnDisable()
    //{
    //    if (imageManager != null)
    //    {
    //        // Use RemoveListener instead of -=
    //        imageManager.trackablesChanged.RemoveListener(OnTrackablesChanged);
    //    }
    //}


    //// This MUST match the generic ARTrackablesChangedEventArgs type
    //private async void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    //{
    //    foreach (var image in eventArgs.added)
    //    {
    //        if (image.trackingState == TrackingState.Tracking)
    //        {
    //            var result = await anchorManager.TryAddAnchorAsync(new Pose(image.transform.position, image.transform.rotation));

    //            if (result.status.IsSuccess())
    //            {
    //                Instantiate(contentPrefab, result.value.transform);
    //            }
    //        }
    //    }
    //}
}
