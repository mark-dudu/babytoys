namespace BabyToys.Models;

public enum ChildLockState
{
    Starting,
    ActiveImage,
    ActiveBlack,
    Unlocking,
    Timeout,
    Sleeping,
    SleepFailedBlack,
    Ended
}
