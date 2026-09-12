using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace CompanyTaskManagement.Models
{
    public class InternYouTubeReference
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Video title is required")]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "YouTube URL is required")]
        [StringLength(500)]
        public string YouTubeUrl { get; set; } = string.Empty;

        [StringLength(50)]
        public string YouTubeVideoId { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Track { get; set; } = "Backend .NET / C#";

        [Required]
        [StringLength(50)]
        public string Difficulty { get; set; } = "Beginner";

        [StringLength(150)]
        public string? ChannelOrMentorName { get; set; } = "Auxinzio Mentors";

        public int DurationMinutes { get; set; } = 30;

        [Required(ErrorMessage = "Key learning takeaways / description is required")]
        public string Description { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Tags { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public static string ExtractVideoId(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;
            url = url.Trim();

            var regex = new Regex(@"(?:youtu\.be\/|youtube\.com\/(?:embed\/|v\/|watch\?v=|watch\?.+&v=|shorts\/))([\w-]{11})", RegexOptions.IgnoreCase);
            var match = regex.Match(url);
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value;
            }

            if (url.Length == 11 && Regex.IsMatch(url, @"^[a-zA-Z0-9_-]{11}$"))
            {
                return url;
            }

            return string.Empty;
        }

        public string ThumbnailUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(YouTubeVideoId))
                {
                    return $"https://img.youtube.com/vi/{YouTubeVideoId}/hqdefault.jpg";
                }
                return "/images/it_software_cloud.jpg";
            }
        }

        public string EmbedUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(YouTubeVideoId))
                {
                    return $"https://www.youtube.com/embed/{YouTubeVideoId}";
                }
                return string.Empty;
            }
        }
    }
}
