using FluentAssertions;
using Garupan.Client.Ui.Screens.Settings.Multiplayer;
using Xunit;

namespace Garupan.Client.Ui.Tests.Screens.Settings.Multiplayer;

/// <summary>
/// Pure value-type coverage for <see cref="TextInputField"/>: typing, backspace, cursor
/// arithmetic, character-class filtering, max-length cap. Exercised through the
/// <see cref="TextInputFilters.Host"/> filter (the more permissive set) except where
/// the port-digits filter is the point under test.
/// </summary>
public sealed class TextInputFieldTests
{
    private static TextInputField NewHost(string value = "") =>
        TextInputField.From(value, maxLength: 16, charFilter: TextInputFilters.Host);

    [Fact]
    public void Empty_field_has_no_value_and_a_cursor_at_zero()
    {
        var field = TextInputField.Empty(8, TextInputFilters.Host);

        field.Value.Should().Be(string.Empty);
        field.Cursor.Should().Be(0);
        field.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void From_value_parks_cursor_at_the_end()
    {
        var field = TextInputField.From("abc", 16, TextInputFilters.Host);

        field.Value.Should().Be("abc");
        field.Cursor.Should().Be(3);
    }

    [Fact]
    public void From_value_strips_characters_outside_the_filter()
    {
        var field = TextInputField.From("ab@c!d", 16, TextInputFilters.Host);

        field.Value.Should().Be("abcd", "the host filter only accepts digits / lowercase / dot / hyphen");
    }

    [Fact]
    public void From_value_truncates_to_max_length()
    {
        var field = TextInputField.From("abcdefghij", 4, TextInputFilters.Host);

        field.Value.Should().Be("abcd");
        field.Cursor.Should().Be(4);
    }

    [Fact]
    public void Typing_an_allowed_character_appends_at_cursor()
    {
        var field = NewHost("ab");

        var typed = field.WithTyped('c');

        typed.Value.Should().Be("abc");
        typed.Cursor.Should().Be(3);
    }

    [Fact]
    public void Typing_at_a_mid_string_cursor_inserts()
    {
        var field = NewHost("ac").WithCursor(-1);

        var typed = field.WithTyped('b');

        typed.Value.Should().Be("abc");
        typed.Cursor.Should().Be(2);
    }

    [Fact]
    public void Typing_a_rejected_character_is_silently_dropped()
    {
        var field = NewHost("abc");

        var typed = field.WithTyped('@');

        typed.Should().Be(field, "characters outside the host filter must be ignored");
    }

    [Fact]
    public void Typing_a_null_char_is_silently_dropped()
    {
        var field = NewHost("abc");

        var typed = field.WithTyped('\0');

        typed.Should().Be(field, "the null char models a non-printable key");
    }

    [Fact]
    public void Typing_past_max_length_is_silently_dropped()
    {
        var field = TextInputField.From("abcd", 4, TextInputFilters.Host);

        var typed = field.WithTyped('e');

        typed.Value.Should().Be("abcd");
        typed.Cursor.Should().Be(4);
    }

    [Fact]
    public void Backspace_removes_the_character_left_of_the_cursor()
    {
        var field = NewHost("abc");

        var back = field.WithBackspace();

        back.Value.Should().Be("ab");
        back.Cursor.Should().Be(2);
    }

    [Fact]
    public void Backspace_at_start_of_field_is_a_no_op()
    {
        var field = NewHost("abc").WithCursor(-1).WithCursor(-1).WithCursor(-1);

        var back = field.WithBackspace();

        back.Should().Be(field);
    }

    [Fact]
    public void Cursor_left_moves_one_position_back_then_clamps_at_zero()
    {
        var field = NewHost("ab");

        field.WithCursor(-1).Cursor.Should().Be(1);
        field.WithCursor(-1).WithCursor(-1).Cursor.Should().Be(0);
        field.WithCursor(-1).WithCursor(-1).WithCursor(-1).Cursor.Should().Be(0);
    }

    [Fact]
    public void Cursor_right_clamps_at_value_length()
    {
        var field = NewHost("ab").WithCursor(-1).WithCursor(-1);

        field.WithCursor(+1).Cursor.Should().Be(1);
        field.WithCursor(+1).WithCursor(+1).Cursor.Should().Be(2);
        field.WithCursor(+1).WithCursor(+1).WithCursor(+1).Cursor.Should().Be(2);
    }

    [Fact]
    public void WithCursorAtEnd_parks_cursor_at_value_length()
    {
        var field = NewHost("abcd").WithCursor(-1).WithCursor(-1);

        var atEnd = field.WithCursorAtEnd();

        atEnd.Cursor.Should().Be(4);
    }

    [Fact]
    public void Port_filter_accepts_digits_only()
    {
        var field = TextInputField.Empty(5, TextInputFilters.Port);

        field.WithTyped('7').WithTyped('a').WithTyped('7').WithTyped('7')
            .WithTyped('7').Value.Should().Be("7777", "letters must not bleed into the port field");
    }

    [Fact]
    public void Host_filter_accepts_lowercase_letters_digits_dot_and_hyphen()
    {
        var field = NewHost();

        var typed = field.WithTyped('g').WithTyped('a').WithTyped('m').WithTyped('e')
            .WithTyped('-').WithTyped('1').WithTyped('.').WithTyped('e').WithTyped('u');

        typed.Value.Should().Be("game-1.eu");
    }
}
