using System.ComponentModel.DataAnnotations;

namespace Web.HMD.Models
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Lütfen isminizi ve soyisminizi giriniz.")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "İsim en az 3, en fazla 50 karakter olabilir.")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "E-Posta adresi zorunludur.")]
        [EmailAddress(ErrorMessage = "Lütfen geçerli bir e-posta adresi giriniz.")]
        public string Email { get; set; }

        [Phone(ErrorMessage = "Lütfen geçerli bir telefon formatı giriniz.")]
        public string Phone { get; set; }

        [Required(ErrorMessage = "Şifre zorunludur.")]
        [MinLength(6, ErrorMessage = "Şifreniz en az 6 karakterden oluşmalıdır.")]
        public string Password { get; set; }
    }
}
