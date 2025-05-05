﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FeEeTDemo.Models
{
    public class CreateEventViewModel
    {
        [Required(ErrorMessage = "Başlık alanı zorunludur.")]
        [MaxLength(100)]
        public string Title { get; set; }

        [Required(ErrorMessage = "Açıklama alanı zorunludur.")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Konum alanı zorunludur.")]
        public string Location { get; set; }

        [Required(ErrorMessage = "Son oy verme tarihi gereklidir")]
        [Display(Name = "Son Oy Verme Tarihi")]
        public DateTime? VotingDeadline { get; set; }

        public bool IsActive { get; set; } = true;

        [Required(ErrorMessage = "En az bir tarih seçeneği eklemelisiniz.")]
        public List<DateTime> DateTimeOptions { get; set; }
    }
}
