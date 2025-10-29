using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LlamaForge.Models
{
    public class ChatMessage : INotifyPropertyChanged
    {
        private string _role = string.Empty;
        private string _content = string.Empty;
        private DateTime _timestamp = DateTime.Now;

        public string Role
        {
            get => _role;
            set
            {
                if (_role != value)
                {
                    _role = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsUser));
                }
            }
        }

        public string Content
        {
            get => _content;
            set
            {
                if (_content != value)
                {
                    _content = value;
                    OnPropertyChanged();
                }
            }
        }

        public DateTime Timestamp
        {
            get => _timestamp;
            set
            {
                if (_timestamp != value)
                {
                    _timestamp = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsUser => Role == "user";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
