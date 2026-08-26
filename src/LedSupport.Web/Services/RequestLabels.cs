namespace LedSupport.Web.Services;

public static class RequestLabels
{
    public static string Status(string? value) => value switch
    {
        "open" => "Açık",
        "in_progress" => "İşlemde",
        "waiting_customer" => "Müşteri bekleniyor",
        "resolved" => "Çözüldü",
        "closed" => "Kapalı",
        _ => value ?? "-"
    };

    public static string Category(string? value) => value switch
    {
        "ariza" => "Arıza",
        "kurulum" => "Kurulum",
        "yazilim" => "Yazılım",
        "genel" => "Genel",
        _ => value ?? "-"
    };
}
