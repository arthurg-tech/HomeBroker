import type { ClipboardEvent, InputHTMLAttributes } from 'react'

type NumericInputProps = Omit<InputHTMLAttributes<HTMLInputElement>, 'type' | 'inputMode' | 'value' | 'onChange'> & {
  value: string
  decimalPlaces?: 0 | 2
  onValueChange: (value: string) => void
}

function formatDigits(digits: string, decimalPlaces: 0 | 2) {
  if (decimalPlaces === 2) {
    const padded = digits.padStart(3, '0')
    return `${padded.slice(0, -2)},${padded.slice(-2)}`
  }

  return digits.replace(/\B(?=(\d{3})+(?!\d))/g, '.')
}

function movePriceCaretToEnd(input: HTMLInputElement) {
  if (input.selectionStart === input.selectionEnd && input.selectionEnd !== input.value.length) {
    input.setSelectionRange(input.value.length, input.value.length)
  }
}

export function NumericInput({ value, decimalPlaces = 0, onValueChange, ...props }: NumericInputProps) {
  const displayValue = formatDigits(value.replace(/\D/g, '').replace(/^0+(?=\d)/, ''), decimalPlaces)

  function updateInput(input: HTMLInputElement, entered: string) {
    const digits = entered.replace(/\D/g, '').replace(/^0+(?=\d)/, '')

    if (digits.length > 5 || /[^\d.,\s]/.test(entered)) {
      input.value = displayValue
      input.setSelectionRange(displayValue.length, displayValue.length)
      return
    }

    const formatted = formatDigits(digits, decimalPlaces)
    let caret = formatted.length

    if (decimalPlaces === 0) {
      let digitsToRight = entered.slice(input.selectionStart ?? entered.length).replace(/\D/g, '').length
      while (caret > 0 && digitsToRight > 0) {
        caret -= 1
        if (/\d/.test(formatted[caret])) digitsToRight -= 1
      }
    }

    input.value = formatted
    input.setSelectionRange(caret, caret)
    onValueChange(decimalPlaces === 2 ? formatted : digits)
  }

  function handlePaste(event: ClipboardEvent<HTMLInputElement>) {
    const pasted = event.clipboardData.getData('text').trim()

    if (decimalPlaces === 2) {
      const amount = /^(?:R\$\s*)?(\d+)[.,](\d{1,2})$/.exec(pasted)
      if (amount) {
        event.preventDefault()
        updateInput(event.currentTarget, amount[1] + amount[2].padEnd(2, '0'))
        return
      }
    }

    const validPaste = decimalPlaces === 0 ? /^(?:\d+|\d{1,2}\.\d{3})$/ : /^\d+$/
    if (!validPaste.test(pasted)) event.preventDefault()
  }

  return (
    <input {...props} type="text" inputMode="numeric" value={displayValue}
      onChange={(event) => updateInput(event.currentTarget, event.currentTarget.value)} onPaste={handlePaste}
      onFocus={decimalPlaces === 2 ? (event) => movePriceCaretToEnd(event.currentTarget) : undefined}
      onClick={decimalPlaces === 2 ? (event) => movePriceCaretToEnd(event.currentTarget) : undefined}
      onSelect={decimalPlaces === 2 ? (event) => movePriceCaretToEnd(event.currentTarget) : undefined} />
  )
}
